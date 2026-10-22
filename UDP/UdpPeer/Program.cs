using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

internal class Program
{
    private static readonly object StateLock = new();
    private static readonly Dictionary<string, Position> RemotePositions = new();
    private static readonly List<string> History = new();
    private static string _myId = "";
    private static string _otherId = "";
    private static int _x, _y;
    private static long _sequence;
    private static long _sent, _received, _applied, _stale, _invalid, _sendErrors;
    private static long _lastSentSequence;

    private sealed record Position(int X, int Y, long Sequence);

    private static async Task Main(string[] args)
    {
        if (args.Length != 5 || (args[0] != "A" && args[0] != "B") ||
            !IPAddress.TryParse(args[1], out IPAddress? localIp) ||
            !int.TryParse(args[2], out int localPort) || localPort is < 1 or > 65535 ||
            !IPAddress.TryParse(args[3], out IPAddress? remoteIp) ||
            !int.TryParse(args[4], out int remotePort) || remotePort is < 1 or > 65535)
        {
            Console.WriteLine("사용법: dotnet run -- <A|B> <내IP> <내포트> <상대IP> <상대포트>");
            Console.WriteLine("예: dotnet run -- A 127.0.0.1 9000 127.0.0.1 9001");
            return;
        }

        _myId = args[0];
        _otherId = _myId == "A" ? "B" : "A";
        using UdpClient peer = new(new IPEndPoint(localIp, localPort));
        IPEndPoint remoteEndPoint = new(remoteIp, remotePort);
        using CancellationTokenSource cts = new();
        Task receiveTask = ReceiveMessagesAsync(peer, remoteEndPoint, cts.Token);

        Console.WriteLine($"{_myId}: {localIp}:{localPort} -> {remoteIp}:{remotePort}");
        Console.WriteLine("명령: X Y | show | stats | old | duplicate | invalid | burst 개수 간격ms | exit");
        ShowState();

        while (true)
        {
            Console.Write("\n> ");
            string? input = Console.ReadLine();
            if (input == null || input.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase)) break;
            string[] parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;
            string command = parts[0].ToLowerInvariant();

            if (command == "show") ShowState();
            else if (command == "stats") ShowStats();
            else if (command == "old" || command == "duplicate")
            {
                string? message;
                lock (StateLock)
                    message = History.Count == 0 ? null :
                        command == "old" ? History[0] : History[^1];
                if (message == null) Console.WriteLine("송신 기록이 없습니다.");
                else
                {
                    await SendRawAsync(peer, remoteEndPoint, message);
                    Console.WriteLine($"[시험 재전송] {message}");
                }
            }
            else if (command == "invalid")
            {
                await SendRawAsync(peer, remoteEndPoint, "");
                await SendRawAsync(peer, remoteEndPoint, "BROKEN|hello");
                await SendRawAsync(peer, remoteEndPoint, "A|abc|1|2");
                Console.WriteLine("잘못된 메시지 3개 전송");
            }
            else if (command == "burst")
            {
                if (parts.Length != 3 || !int.TryParse(parts[1], out int count) ||
                    !int.TryParse(parts[2], out int interval) || count is < 1 or > 10000 ||
                    interval is < 0 or > 10000)
                {
                    Console.WriteLine("사용법: burst <1~10000> <간격ms 0~10000>");
                    continue;
                }
                long sentBefore = Interlocked.Read(ref _sent);
                long errorBefore = Interlocked.Read(ref _sendErrors);
                long startSeq;
                lock (StateLock) startSeq = _sequence + 1;
                Stopwatch sw = Stopwatch.StartNew();
                for (int i = 0; i < count; i++)
                {
                    int nextX, nextY;
                    lock (StateLock)
                    {
                        // 좌표는 int 범위 내에서 변경 (오버플로 방지)
                        _x = _x == int.MaxValue ? int.MinValue : _x + 1;
                        nextX = _x;
                        nextY = _y;
                    }
                    await SendPositionAsync(peer, remoteEndPoint, nextX, nextY, false);
                    if (interval > 0) await Task.Delay(interval);
                }
                sw.Stop();
                long endSeq;
                lock (StateLock) endSeq = _sequence;
                Console.WriteLine($"[BURST] ID={_myId}, Seq={startSeq}~{endSeq}, " +
                    $"시도={count}, 송신완료={Interlocked.Read(ref _sent) - sentBefore}, " +
                    $"송신오류={Interlocked.Read(ref _sendErrors) - errorBefore}, " +
                    $"시간={sw.ElapsedMilliseconds}ms");
                Console.WriteLine("상대 측 stats/show를 확인하세요. 송신 완료는 수신 보장이 아닙니다.");
            }
            else if (parts.Length == 2 &&
                     int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int x) &&
                     int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int y))
            {
                bool changed;
                lock (StateLock)
                {
                    changed = _x != x || _y != y;
                    if (changed) { _x = x; _y = y; }
                }
                if (!changed) Console.WriteLine("좌표 변화 없음: 전송하지 않음");
                else await SendPositionAsync(peer, remoteEndPoint, x, y, true);
            }
            else Console.WriteLine("잘못된 입력. 예: 3 5 또는 burst 100 10");
        }

        cts.Cancel();
        peer.Close();
        await receiveTask;
        Console.WriteLine("종료");
    }

    private static async Task SendPositionAsync(UdpClient peer, IPEndPoint endpoint, int x, int y, bool print)
    {
        string message;
        lock (StateLock)
        {
            _sequence++;
            message = $"{_myId}|{_sequence}|{x}|{y}";
            History.Add(message);
            _lastSentSequence = _sequence;
        }
        bool ok = await SendRawAsync(peer, endpoint, message);
        if (print) Console.WriteLine(ok ? $"[송신] {message}" : $"[송신 실패] {message}");
    }

    private static async Task<bool> SendRawAsync(UdpClient peer, IPEndPoint endpoint, string message)
    {
        try
        {
            byte[] bytes = Encoding.UTF8.GetBytes(message);
            await peer.SendAsync(bytes, bytes.Length, endpoint);
            Interlocked.Increment(ref _sent);
            return true;
        }
        catch (SocketException ex)
        {
            Interlocked.Increment(ref _sendErrors);
            Console.WriteLine($"[송신 오류] {ex.Message}");
            return false;
        }
    }

    private static async Task ReceiveMessagesAsync(UdpClient peer, IPEndPoint expectedEndpoint, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            UdpReceiveResult result;
            try { result = await peer.ReceiveAsync(token); }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (SocketException ex)
            {
                if (token.IsCancellationRequested) break;
                Console.WriteLine($"[수신 소켓 오류] {ex.Message}");
                continue;
            }
            Interlocked.Increment(ref _received);
            if (!result.RemoteEndPoint.Equals(expectedEndpoint))
            {
                Interlocked.Increment(ref _invalid);
                Console.WriteLine("[거절] 예상하지 않은 송신 주소/포트");
                continue;
            }
            string message = Encoding.UTF8.GetString(result.Buffer);
            string[] parts = message.Split('|');
            if (parts.Length != 4 || parts[0] != _otherId ||
                !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out long seq) || seq <= 0 ||
                !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int x) ||
                !int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int y))
            {
                Interlocked.Increment(ref _invalid);
                Console.WriteLine($"[잘못된 메시지] {message}");
                continue;
            }
            bool accepted;
            lock (StateLock)
            {
                accepted = !RemotePositions.TryGetValue(parts[0], out Position? previous) || seq > previous.Sequence;
                if (accepted) RemotePositions[parts[0]] = new Position(x, y, seq);
            }
            if (accepted)
            {
                Interlocked.Increment(ref _applied);
                Console.WriteLine($"\n[상대 위치 반영] {parts[0]}: ({x}, {y}), Seq={seq}");
            }
            else
            {
                Interlocked.Increment(ref _stale);
                Console.WriteLine($"\n[오래된/중복 메시지 무시] {parts[0]}, Seq={seq}");
            }
        }
    }

    private static void ShowState()
    {
        lock (StateLock)
        {
            Console.WriteLine($"[내 상태] {_myId}: ({_x}, {_y}), 마지막 생성 Seq={_lastSentSequence}");
            if (RemotePositions.TryGetValue(_otherId, out Position? other))
                Console.WriteLine($"[상대 상태] {_otherId}: ({other.X}, {other.Y}), Seq={other.Sequence}");
            else Console.WriteLine($"[상대 상태] {_otherId}: 미수신");
        }
    }

    private static void ShowStats()
    {
        Console.WriteLine($"[통계] 송신완료={Interlocked.Read(ref _sent)}, 송신오류={Interlocked.Read(ref _sendErrors)}, " +
            $"수신={Interlocked.Read(ref _received)}, 위치반영={Interlocked.Read(ref _applied)}, " +
            $"오래된/중복무시={Interlocked.Read(ref _stale)}, 잘못된메시지={Interlocked.Read(ref _invalid)}");
        ShowState();
    }
}
