# GameNetwork — NGO 코인 게임 및 UDP 피어 통신

## 1. 프로젝트 소개

Unity Netcode for GameObjects(NGO)를 이용한 코인 획득 게임과 C# UDP 피어 통신 프로그램을 구현한 네트워크 실습 프로젝트입니다.

두 프로그램은 별도로 실행하며, 같은 컴퓨터에서 네트워크 통신을 시험할 수 있도록 구성했습니다.

### 구현 목표

- NGO를 이용한 Host/Client 접속 및 플레이어 상태 동기화
- 서버에서 코인 획득 요청 검증 및 점수 처리
- UDP를 이용한 양방향 위치 데이터 송수신
- 순서 번호를 이용한 최신 위치 반영
- 잘못된 요청과 메시지 처리 및 전송량 시험

## 2. 개발 환경

| 항목 | 버전 및 설정 |
|---|---|
| Unity Editor | Unity 6 (6000.6.6f1, 최종 확인 필요) |
| Netcode for GameObjects | 2.13.3 |
| Unity Transport | Unity 프로젝트 Package Manager 설정 참고 |
| Input System | Unity Input System |
| UI | Unity UGUI |
| C# 콘솔 | .NET 8.0 (설치 SDK 확인 필요) |
| 실행 환경 | Windows, 동일 PC에서 Host/Client 및 UDP A/B 실행 |

Unity 패키지는 `CoinGame/Packages/manifest.json`을 기준으로 복원합니다.

## 3. 프로젝트 구조

```text
김재우_GameNetwork/
├── README.md
├── DESIGN.md
├── TEST_REPORT.md
├── CoinGame/
│   ├── Assets/
│   ├── Packages/
│   └── ProjectSettings/
├── UdpPeer/
│   ├── Program.cs
│   └── UdpPeer.csproj
└── Evidence/
```

실제 제출 폴더 구조에 따라 경로를 확인해야 합니다.

## 4. NGO 코인 게임 실행 방법

### 4.1 프로젝트 열기

1. Unity Hub를 실행합니다.
2. `CoinGame` 폴더를 프로젝트로 추가합니다.
3. 수업에서 지정한 Unity 6 버전으로 프로젝트를 엽니다.
4. 필요한 패키지가 복원될 때까지 기다립니다.
5. `Assets`에서 게임 실행 씬을 엽니다.

시작 씬: **[실제 씬 경로 입력]**

### 4.2 Host 및 Client 실행

1. Unity Editor에서 게임 씬을 실행합니다.
2. Host 버튼을 눌러 Host를 시작합니다.
3. 별도의 실행 환경에서 동일한 프로젝트의 Client를 실행합니다.
4. Client 버튼을 눌러 접속합니다.
5. 양쪽 화면에 플레이어가 생성되는지 확인합니다.

접속 설정:

- 주소: `127.0.0.1` (동일 PC 기준)
- 포트: **[Unity Transport 설정 포트 입력]**
- 설정 위치: Unity의 `NetworkManager`에 연결된 Unity Transport 컴포넌트

Host와 Client를 같은 컴퓨터에서 실행할 때는 Unity Editor와 별도의 빌드 실행 파일 등을 이용할 수 있습니다.

### 4.3 조작 방법

| 조작 | 기능 |
|---|---|
| WASD | 본인 플레이어 이동 |
| Host 버튼 | Host 시작 |
| Client 버튼 | Client 접속 |
| AddScore 버튼 | 점수 증가 요청 시험 |
| 코인 접촉 | 서버에 코인 획득 요청 |

### 4.4 정상 실행 확인

- Host와 Client가 각각 접속됩니다.
- 각 참여자는 자신이 소유한 플레이어를 조작할 수 있습니다.
- 플레이어 이동 결과가 상대 실행 화면에도 표시됩니다.
- 플레이어가 코인에 접촉하면 서버에 획득 요청을 보냅니다.
- 서버가 요청을 검증하고 점수를 증가시킵니다.
- 획득된 코인은 제거되고 참여자에게 결과가 동기화됩니다.

## 5. UDP 피어 통신 실행 방법

### 5.1 프로젝트 빌드

PowerShell에서 `UdpPeer.csproj`가 있는 폴더로 이동합니다.

```powershell
dotnet restore
dotnet build
```

### 5.2 피어 A 실행

첫 번째 PowerShell 창에서 다음 명령어를 입력합니다.

```powershell
dotnet run -- 9000 9001 A
```

### 5.3 피어 B 실행

두 번째 PowerShell 창에서 다음 명령어를 입력합니다.

```powershell
dotnet run -- 9001 9000 B
```

각 인자의 의미는 다음과 같습니다.

```text
dotnet run -- <내 포트> <상대 포트> <이름>
```

- 내 포트: UDP 메시지를 수신할 포트
- 상대 포트: UDP 메시지를 전송할 대상 포트
- 이름: 송신 피어를 구분하는 식별값

두 프로그램 모두 `127.0.0.1`(Loopback)을 사용하므로 같은 컴퓨터에서 실행합니다.

주소를 변경하려면 `Program.cs`의 `IPAddress.Loopback` 설정을 수정해야 합니다.

### 5.4 UDP 명령어

| 명령어 | 기능 |
|---|---|
| `3 5` | X=3, Y=5 좌표 전송 |
| `/old` | 이전 메시지 재전송 시험 |
| `/invalid` | 잘못된 메시지 전송 시험 |
| `/burst 100 10` | 10ms 간격으로 100회 송신 |
| `/burst 1000 0` | 지연 없이 1000회 송신 |
| `/stats` | 송수신 통계 및 현재 위치 출력 |
| `/exit` | 프로그램 종료 |

위 명령어는 최종 UDP 코드에 포함된 기능을 기준으로 작성했습니다.

### 5.5 UDP 정상 실행 확인

피어 A에서 다음을 입력합니다.

```text
3 5
```

피어 B에서 다음과 같은 출력이 나타나면 위치 데이터가 정상적으로 수신된 것입니다.

```text
[상대 위치] A: (3, 5), Seq=1
```

이후 A에서 다른 좌표를 입력하면 B의 상대 위치가 갱신됩니다.

`/old` 명령어로 이전 순서 번호의 메시지를 다시 보내면 수신 측에서 무시해야 합니다.

`/invalid` 명령어로 잘못된 메시지를 보내더라도 프로그램이 종료되지 않아야 합니다.

## 6. 시험 방법

### NGO 시험

- Host와 Client 동시 접속
- 각 플레이어의 소유권에 따른 입력 처리
- 플레이어 이동 동기화
- 정상 코인 획득 및 점수 증가
- 잘못된 코인 ID 요청 거절
- 거리 조건을 만족하지 않는 요청 거절
- 중복 코인 획득 요청 거절
- Client 종료 및 재접속 후 상태 확인

### UDP 시험

- A/B 양방향 좌표 송수신
- 순서 번호 증가 확인
- 오래된 메시지 및 중복 메시지 무시
- 잘못된 메시지 수신 후 정상 동작 유지
- 서로 다른 전송량 조건의 결과 비교

시험 조건과 실제 결과는 `TEST_REPORT.md`에 기록하고 관련 화면 캡처 및 로그는 `Evidence/` 폴더에 보관합니다.

## 7. 설계 및 참고 자료

수업에서 제공된 NGO 및 UDP 실습 코드를 기반으로 구현했습니다.

NGO에서는 RPC, NetworkVariable, NetworkObject 등의 기능을 활용했으며, UDP에서는 UdpClient와 Task 기반 비동기 송수신 구조를 활용했습니다.

직접 추가하거나 변경한 게임 규칙, 메시지 형식, 서버 검증, 순서 번호 검사 등의 내용은 `DESIGN.md`에 정리합니다.

## 8. 알려진 제한 사항 및 미완성 기능

- UDP 통신은 동일 PC의 Loopback 주소를 사용하는 환경을 기준으로 구현했습니다.
- UDP는 메시지의 도착과 순서를 보장하지 않으므로 순서 번호로 오래된 위치 정보를 구분합니다.
- UDP 전송량 시험의 송신 완료 횟수는 실제 상대방의 수신 성공 횟수를 의미하지 않습니다.
- NGO의 실제 실행 환경과 종료·재접속 시험 결과는 최종 검증 후 `TEST_REPORT.md`에 기록해야 합니다.
- Unity 프로젝트의 버전 확인 및 설정 파일 저장 오류가 발생한 이력이 있어 최종 실행 검증이 필요합니다.
- 기타 미구현 사항: **[최종 확인 후 입력]**

## 9. 제출 전 확인

제출 전 프로젝트를 다른 위치에 복사하여 Unity 프로젝트와 UDP 콘솔 프로젝트를 다시 열고 실행할 수 있는지 확인합니다.

Unity의 `Assets`, `Packages`, `ProjectSettings` 및 `.meta` 파일과 UDP의 `.csproj`, `.cs` 소스 파일을 포함합니다.

`Library`, `Temp`, `Logs`, `.vs`, `bin`, `obj` 등 자동 생성 파일은 제출 대상에서 제외합니다.
