
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

// 실행 인자: 내 ID, 내 IP, 내 포트, 상대 IP, 상대 포트
if (args.Length != 5 ||
    !IPAddress.TryParse(args[1], out IPAddress? localAddress) ||
    !int.TryParse(args[2], out int localPort) ||
    !IPAddress.TryParse(args[3], out IPAddress? remoteAddress) ||
    !int.TryParse(args[4], out int remotePort) ||
    localPort < 1 || localPort > 65535 ||
    remotePort < 1 || remotePort > 65535 ||
    (args[0] != "A" && args[0] != "B"))
{
    Console.WriteLine(
        "사용법: UdpPeer <A/B> <내 IP> <내 포트> <상대 IP> <상대 포트>");
    return;
}

string peerId = args[0];
string expectedRemoteId = peerId == "A" ? "B" : "A";

using UdpClient udp = new UdpClient(
    new IPEndPoint(localAddress, localPort));

IPEndPoint remoteEndPoint =
    new IPEndPoint(remoteAddress, remotePort);

using CancellationTokenSource cts = new();

object stateLock = new();

// 자신의 위치와 송신 순서 번호
int x = 0;
int y = 0;
int sequenceNumber = 0;

// 상대 피어별 최신 위치
Dictionary<string, PositionState> remoteStates = new();

// 송신 기록: 오래된 메시지 재전송 시험용
List<string> sentHistory = new();

// 통계
long sentCount = 0;
long receivedCount = 0;
long appliedCount = 0;
long ignoredCount = 0;
long invalidCount = 0;

Console.WriteLine($"내 피어: {peerId}");
Console.WriteLine($"내 주소: {localAddress}:{localPort}");
Console.WriteLine($"상대 주소: {remoteAddress}:{remotePort}");
Console.WriteLine("UDP 위치 동기화 시작");

// 입력과 독립적으로 수신 작업 실행
Task receiveTask = ReceiveLoopAsync();

PrintHelp();
PrintState();

// 메인 입력 루프
while (true)
{
    Console.Write("\n명령 > ");
    string? input = Console.ReadLine();

    if (input == null || input.Trim().Equals(
        "exit", StringComparison.OrdinalIgnoreCase))
        break;

    string[] parts = input.Split(
        ' ', StringSplitOptions.RemoveEmptyEntries);

    if (parts.Length == 0)
        continue;

    string command = parts[0].ToLowerInvariant();

    if (command == "help")
    {
        PrintHelp();
    }
    else if (command == "show")
    {
        PrintState();
    }
    else if (command == "stats")
    {
        PrintStats();
    }
    else if (command == "old")
    {
        // 첫 번째로 보낸 메시지를 원본 그대로 재전송
        string? oldMessage;

        lock (stateLock)
        {
            oldMessage = sentHistory.Count > 0
                ? sentHistory[0]
                : null;
        }

        if (oldMessage == null)
        {
            Console.WriteLine("송신 기록이 없습니다.");
            continue;
        }

        await SendRawAsync(oldMessage);
        Console.WriteLine($"[오래된 메시지 재전송] {oldMessage}");
    }
    else if (command == "duplicate")
    {
        // 마지막 메시지를 원본 그대로 재전송
        string? lastMessage;

        lock (stateLock)
        {
            lastMessage = sentHistory.Count > 0
                ? sentHistory[^1]
                : null;
        }

        if (lastMessage == null)
        {
            Console.WriteLine("송신 기록이 없습니다.");
            continue;
        }

        await SendRawAsync(lastMessage);
        Console.WriteLine($"[중복 메시지 재전송] {lastMessage}");
    }
    else if (command == "invalid")
    {
        // 잘못된 형식과 빈 메시지 시험
        await SendRawAsync("INVALID|DATA");
        await SendRawAsync("");
        Console.WriteLine("잘못된 메시지 2개 전송");
    }
    else if (command == "burst")
    {
        // burst <전송 개수> <간격(ms)>
        if (parts.Length != 3 ||
            !int.TryParse(parts[1], out int count) ||
            !int.TryParse(parts[2], out int intervalMs) ||
            count < 1 || count > 10000 ||
            intervalMs < 0 || intervalMs > 10000)
        {
            Console.WriteLine("예: burst 100 10");
            continue;
        }

        Stopwatch watch = Stopwatch.StartNew();

        long beforeSent = Interlocked.Read(ref sentCount);

        for (int i = 0; i < count; i++)
        {
            int nextX;
            int nextY;

            lock (stateLock)
            {
                // 시험용 위치 변경
                x++;
                nextX = x;
                nextY = y;
            }

            await SendPositionAsync(nextX, nextY, false);

            if (intervalMs > 0)
                await Task.Delay(intervalMs);
        }

        watch.Stop();

        Console.WriteLine(
            $"[전송량 시험] 요청: {count}, " +
            $"송신 완료: {Interlocked.Read(ref sentCount) - beforeSent}, " +
            $"소요: {watch.ElapsedMilliseconds}ms");

        Console.WriteLine(
            "상대 피어의 stats 결과도 기록하세요.");
    }
    else if (parts.Length == 2 &&
             int.TryParse(parts[0], out int newX) &&
             int.TryParse(parts[1], out int newY))
    {
        bool changed;

        lock (stateLock)
        {
            changed = x != newX || y != newY;

            if (changed)
            {
                x = newX;
                y = newY;
            }
        }

        if (!changed)
        {
            Console.WriteLine("위치가 동일하여 전송하지 않습니다.");
            continue;
        }

        await SendPositionAsync(newX, newY, true);
    }
    else
    {
        Console.WriteLine("알 수 없는 명령입니다. help를 입력하세요.");
    }
}

cts.Cancel();
udp.Close();

try
{
    await receiveTask;
}
catch (OperationCanceledException)
{
}

Console.WriteLine("프로그램 종료");


// 새로운 위치 메시지 송신
async Task SendPositionAsync(int px, int py, bool print)
{
    string message;

    lock (stateLock)
    {
        sequenceNumber++;

        message = $"{peerId}|{sequenceNumber}|{px}|{py}";
        sentHistory.Add(message);
    }

    await SendRawAsync(message);

    if (print)
        Console.WriteLine($"[송신] {message}");
}


// UDP 전송
async Task SendRawAsync(string message)
{
    try
    {
        byte[] data = Encoding.UTF8.GetBytes(message);

        await udp.SendAsync(data, remoteEndPoint);

        Interlocked.Increment(ref sentCount);
    }
    catch (Exception ex) when (
        ex is SocketException ||
        ex is ObjectDisposedException)
    {
        Console.WriteLine($"[송신 오류] {ex.Message}");
    }
}


// UDP 수신 반복
async Task ReceiveLoopAsync()
{
    while (!cts.IsCancellationRequested)
    {
        UdpReceiveResult result;

        try
        {
            result = await udp.ReceiveAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            break;
        }
        catch (ObjectDisposedException)
        {
            break;
        }
        catch (SocketException ex)
        {
            if (cts.IsCancellationRequested)
                break;

            Console.WriteLine($"[수신 오류] {ex.Message}");
            continue;
        }

        Interlocked.Increment(ref receivedCount);

        // 예상한 상대 주소와 포트인지 확인
        if (!result.RemoteEndPoint.Equals(remoteEndPoint))
        {
            Interlocked.Increment(ref invalidCount);
            Console.WriteLine("[거절] 알 수 없는 송신 주소");
            continue;
        }

        string message = Encoding.UTF8.GetString(result.Buffer);

        if (!TryParsePosition(
                message,
                out string senderId,
                out int seq,
                out int rx,
                out int ry))
        {
            Interlocked.Increment(ref invalidCount);
            Console.WriteLine($"[잘못된 메시지] {message}");
            continue;
        }

        if (senderId != expectedRemoteId)
        {
            Interlocked.Increment(ref invalidCount);
            Console.WriteLine($"[거절] 예상하지 않은 피어: {senderId}");
            continue;
        }

        bool applied;

        lock (stateLock)
        {
            if (remoteStates.TryGetValue(
                    senderId, out PositionState? previous) &&
                seq <= previous.Sequence)
            {
                applied = false;
            }
            else
            {
                remoteStates[senderId] =
                    new PositionState(seq, rx, ry);

                applied = true;
            }
        }

        if (!applied)
        {
            Interlocked.Increment(ref ignoredCount);
            Console.WriteLine(
                $"[무시] {senderId}, Seq={seq}, 오래되거나 중복된 메시지");
            continue;
        }

        Interlocked.Increment(ref appliedCount);

        Console.WriteLine(
            $"\n[위치 반영] {senderId}: ({rx}, {ry}), Seq={seq}");
    }
}


// 위치 메시지 파싱
bool TryParsePosition(
    string message,
    out string senderId,
    out int seq,
    out int px,
    out int py)
{
    senderId = "";
    seq = 0;
    px = 0;
    py = 0;

    if (string.IsNullOrWhiteSpace(message))
        return false;

    string[] fields = message.Split('|');

    if (fields.Length != 4)
        return false;

    senderId = fields[0];

    if (senderId != "A" && senderId != "B")
        return false;

    return int.TryParse(fields[1], out seq) &&
           seq > 0 &&
           int.TryParse(fields[2], out px) &&
           int.TryParse(fields[3], out py);
}


// 내 위치와 상대 위치 표시
void PrintState()
{
    lock (stateLock)
    {
        Console.WriteLine(
            $"\n[내 상태] {peerId}: ({x}, {y}), Seq={sequenceNumber}");

        if (remoteStates.TryGetValue(
                expectedRemoteId, out PositionState? remote))
        {
            Console.WriteLine(
                $"[상대 상태] {expectedRemoteId}: " +
                $"({remote.X}, {remote.Y}), Seq={remote.Sequence}");
        }
        else
        {
            Console.WriteLine(
                $"[상대 상태] {expectedRemoteId}: 미수신");
        }
    }
}


// 송수신 통계
void PrintStats()
{
    Console.WriteLine("\n[통신 통계]");
    Console.WriteLine($"송신 완료: {Interlocked.Read(ref sentCount)}");
    Console.WriteLine($"수신 데이터그램: {Interlocked.Read(ref receivedCount)}");
    Console.WriteLine($"위치 반영: {Interlocked.Read(ref appliedCount)}");
    Console.WriteLine($"오래된/중복 무시: {Interlocked.Read(ref ignoredCount)}");
    Console.WriteLine($"잘못된 메시지: {Interlocked.Read(ref invalidCount)}");

    PrintState();
}


// 명령 안내
void PrintHelp()
{
    Console.WriteLine("\n=== 명령어 ===");
    Console.WriteLine("3 5             : 내 위치를 (3,5)로 변경");
    Console.WriteLine("show            : 내 위치와 상대 위치 표시");
    Console.WriteLine("stats           : 통신 통계");
    Console.WriteLine("old             : 첫 송신 메시지 재전송");
    Console.WriteLine("duplicate       : 마지막 송신 메시지 재전송");
    Console.WriteLine("invalid         : 잘못된 메시지 시험");
    Console.WriteLine("burst 100 10    : 10ms 간격으로 100개 송신");
    Console.WriteLine("burst 1000 0    : 지연 없이 1000개 송신");
    Console.WriteLine("exit            : 종료");
}


// 상대 위치 데이터
public record PositionState(int Sequence, int X, int Y);
