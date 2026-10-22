
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

class Program
{
    // 내 정보
    private static string _myName = "";
    private static int _x = 0;
    private static int _y = 0;
    private static uint _sequenceId = 0;

    // 상대 정보
    private static string _remoteName = "";
    private static int _remoteX = 0;
    private static int _remoteY = 0;
    private static uint _lastReceivedSequenceId = 0;

    // 시험용 정보
    private static string _oldMessage = "";
    private static int _sentCount = 0;
    private static int _receivedCount = 0;
    private static int _appliedCount = 0;
    private static int _ignoredCount = 0;
    private static int _invalidCount = 0;

    static async Task Main(string[] args)
    {
        if (args.Length != 3
            || !int.TryParse(args[0], out int localPort)
            || !int.TryParse(args[1], out int remotePort)
            || localPort < 1 || localPort > 65535
            || remotePort < 1 || remotePort > 65535
            || string.IsNullOrWhiteSpace(args[2]))
        {
            Console.WriteLine(
                "사용법: dotnet run -- <내 포트> <상대 포트> <이름>");
            return;
        }

        _myName = args[2];

        using (UdpClient peer = new UdpClient(
            new IPEndPoint(IPAddress.Loopback, localPort)))
        {
            IPEndPoint destination =
                new IPEndPoint(IPAddress.Loopback, remotePort);

            Console.WriteLine(
                $"{_myName}: 내 포트 {localPort}, 상대 포트 {remotePort}");

            Console.WriteLine("좌표 입력: X Y");
            Console.WriteLine(
                "명령어: /old, /invalid, /burst 개수 간격ms, /stats, /exit");

            // 수신과 송신을 동시에 실행
            Task receiveTask = ReceiveMessagesAsync(peer);

            Task sendTask = Task.Run(() =>
                SendMessagesAsync(peer, destination));

            await Task.WhenAll(sendTask, receiveTask);
        }
    }

    private static async Task SendMessagesAsync(
        UdpClient peer, IPEndPoint destination)
    {
        try
        {
            while (true)
            {
                Console.Write("> ");

                string? input = Console.ReadLine();

                if (input == null || input == "/exit")
                    return;

                // 통계 확인
                if (input == "/stats")
                {
                    Console.WriteLine(
                        $"송신: {_sentCount}, 수신: {_receivedCount}, " +
                        $"반영: {_appliedCount}, 무시: {_ignoredCount}, " +
                        $"잘못된 메시지: {_invalidCount}");

                    Console.WriteLine(
                        $"내 위치: ({_x}, {_y})");

                    Console.WriteLine(
                        $"상대 위치: {_remoteName} ({_remoteX}, {_remoteY})");

                    continue;
                }

                // 이전 메시지를 다시 보내는 시험
                if (input == "/old")
                {
                    if (_oldMessage == "")
                    {
                        Console.WriteLine("이전에 보낸 메시지가 없습니다.");
                        continue;
                    }

                    byte[] oldBytes =
                        Encoding.UTF8.GetBytes(_oldMessage);

                    await peer.SendAsync(
                        oldBytes, oldBytes.Length, destination);

                    _sentCount++;

                    Console.WriteLine(
                        $"[이전 메시지 재전송] {_oldMessage}");

                    continue;
                }

                // 잘못된 메시지 시험
                if (input == "/invalid")
                {
                    byte[] bytes =
                        Encoding.UTF8.GetBytes("잘못된 메시지");

                    await peer.SendAsync(
                        bytes, bytes.Length, destination);

                    _sentCount++;

                    Console.WriteLine("잘못된 메시지 전송");
                    continue;
                }

                string[] parts = input.Split(
                    ' ', StringSplitOptions.RemoveEmptyEntries);

                // 전송량 시험
                if (parts.Length == 3 && parts[0] == "/burst")
                {
                    if (!int.TryParse(parts[1], out int count) ||
                        !int.TryParse(parts[2], out int delay) ||
                        count < 1 || count > 10000 ||
                        delay < 0 || delay > 10000)
                    {
                        Console.WriteLine(
                            "사용법: /burst <1~10000> <간격ms 0~10000>");
                        continue;
                    }

                    Stopwatch stopwatch = Stopwatch.StartNew();

                    for (int i = 0; i < count; i++)
                    {
                        // 매번 좌표를 변경해서 전송
                        _x++;

                        await SendPositionAsync(
                            peer, destination, false);

                        if (delay > 0)
                            await Task.Delay(delay);
                    }

                    stopwatch.Stop();

                    Console.WriteLine(
                        $"[전송량 시험] {count}회, " +
                        $"{stopwatch.ElapsedMilliseconds}ms");

                    continue;
                }

                // X Y 좌표 입력
                if (parts.Length != 2 ||
                    !int.TryParse(parts[0], out int x) ||
                    !int.TryParse(parts[1], out int y))
                {
                    Console.WriteLine(
                        "좌표를 X Y 형식으로 입력하세요.");
                    continue;
                }

                _x = x;
                _y = y;

                await SendPositionAsync(
                    peer, destination, true);
            }
        }
        catch (SocketException exception)
        {
            Console.WriteLine(
                $"송신 실패: {exception.Message}");
        }
        finally
        {
            peer.Close();
        }
    }

    private static async Task SendPositionAsync(
        UdpClient peer, IPEndPoint destination, bool print)
    {
        // 메시지 형식: 이름|순서번호|X|Y
        _sequenceId++;

        string message =
            $"{_myName}|{_sequenceId}|{_x}|{_y}";

        // 시험용으로 이전 메시지를 보관
        if (_sequenceId == 1)
            _oldMessage = message;

        byte[] bytes = Encoding.UTF8.GetBytes(message);

        await peer.SendAsync(
            bytes, bytes.Length, destination);

        _sentCount++;

        if (print)
        {
            Console.WriteLine(
                $"전송: {message}");
        }
    }

    private static async Task ReceiveMessagesAsync(UdpClient peer)
    {
        try
        {
            while (true)
            {
                UdpReceiveResult result =
                    await peer.ReceiveAsync();

                _receivedCount++;

                string message =
                    Encoding.UTF8.GetString(result.Buffer);

                // 이름|순서번호|X|Y
                string[] parts = message.Split('|');

                // 잘못된 메시지 검사
                if (parts.Length != 4 ||
                    string.IsNullOrWhiteSpace(parts[0]) ||
                    !uint.TryParse(parts[1], out uint sequenceId) ||
                    sequenceId == 0 ||
                    !int.TryParse(parts[2], out int x) ||
                    !int.TryParse(parts[3], out int y))
                {
                    _invalidCount++;

                    Console.WriteLine(
                        $"[잘못된 메시지] {message}");

                    continue;
                }

                // 다른 송신자 이름이 들어오면 무시
                if (_remoteName != "" &&
                    _remoteName != parts[0])
                {
                    _invalidCount++;
                    Console.WriteLine(
                        $"[다른 송신자 무시] {parts[0]}");
                    continue;
                }

                if (parts[0] == _myName)
                {
                    _invalidCount++;
                    Console.WriteLine(
                        "[내 이름과 같은 메시지 무시]");
                    continue;
                }

                // 오래되거나 중복된 순서 번호 검사
                if (sequenceId <= _lastReceivedSequenceId)
                {
                    _ignoredCount++;

                    Console.WriteLine(
                        $"[오래된 메시지 무시] Seq={sequenceId}");

                    continue;
                }

                // 정상적인 최신 메시지만 상대 위치에 반영
                _lastReceivedSequenceId = sequenceId;

                _remoteName = parts[0];
                _remoteX = x;
                _remoteY = y;

                _appliedCount++;

                Console.WriteLine(
                    $"\n[상대 위치] {_remoteName}: " +
                    $"({_remoteX}, {_remoteY}), Seq={sequenceId}");
            }
        }
        catch (ObjectDisposedException)
        {
            // 종료 시 소켓이 닫힌 경우
        }
        catch (SocketException exception)
        {
            Console.WriteLine(
                $"수신 종료: {exception.Message}");
        }
    }
}
