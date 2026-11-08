# Schdule Management with Notion

C# / .NET / WPF로 만드는 Windows 일정 관리 앱입니다.

## 목표

- MVVM과 CommunityToolkit.Mvvm을 활용한 화면 구성
- HttpClient / System.Text.Json으로 Notion REST API에 직접 연결
- 사용자가 자신의 Notion Integration 토큰을 등록하는 BYOK 방식
- Windows DPAPI로 로컬 토큰 보호
- 별도 백엔드 서버 없이 동작
- Windows 시작 시 자동 실행 및 시스템 트레이 상주
- GitHub 공개 저장소를 통한 배포

현재는 앱을 시작할 때 Notion에서 일정을 읽어 표에 표시합니다. 연결 설정이 없으면 예시 일정이 표시됩니다.

## 현재 폴더 구조

    Views/
        MainWindow.xaml       화면 레이아웃과 스타일
        MainWindow.xaml.cs    화면 초기화와 화면 이벤트
    ViewModels/
        MainViewModel.cs      화면에 바인딩할 일정 목록
    Models/
        ScheduleItem.cs       일정 한 건의 데이터 구조
    App.xaml                  앱 시작 화면과 공통 리소스
    App.xaml.cs                앱 수명 주기 진입점

Views는 Vue의 template, ViewModels는 화면 상태와 동작을 담는 script와
비슷한 역할입니다. Models는 Java의 데이터 클래스 또는 record에 가깝습니다.
C#의 namespace는 Java의 package처럼 타입을 구분합니다.
폴더 이동에 맞춰 namespace도 역할별로 나누었습니다.

Notion API 요청과 일정 변환은 Services/Notion에 있습니다.

## 기능을 구현할 때 추가할 폴더

- Services/Security: DPAPI를 이용한 토큰 저장과 읽기
- Services/Windows: 시스템 트레이 및 시작 시 자동 실행
- Resources: 여러 화면이 공유하는 스타일과 아이콘

필요한 코드가 생길 때 폴더를 추가합니다.
현재는 하나의 WPF 프로젝트로 유지합니다.
토큰은 Git에서 제외된 로컬 설정에 두고 로그에 기록하지 않습니다.

## Notion 연결 및 실행

1. Notion Internal Integration에 읽기 권한을 부여합니다. 원본 일정 데이터베이스의 **••• → 연결 추가(Add connections)**에서 Integration을 선택합니다.
2. 프로젝트 파일(.csproj)과 같은 폴더에 UTF-8로 `.env`를 만들고 아래 내용을 입력합니다. 데이터 소스 ID는 데이터베이스 설정의 **Manage data sources → Copy data source ID**에서 복사할 수 있습니다.
3. 프로젝트 폴더에서 `dotnet run`을 실행하거나 Visual Studio에서 F5를 누릅니다.

```dotenv
NOTION_TOKEN=실제_토큰
NOTION_DATA_SOURCE_ID=실제_데이터_소스_ID
```

값은 한 줄에 하나씩 `KEY=value` 형식으로 입력합니다. 빈 줄과 `#`로 시작하는 주석 줄은 건너뜁니다. `.env`는 Git에서 제외됩니다. 배포한 앱은 실행 파일 옆의 `.env`를 읽습니다. 같은 항목에 비어 있지 않은 프로세스 환경변수가 있으면 환경변수 값이 우선합니다. Windows 사용자 환경변수를 바꿨다면 새 터미널을 열거나 Visual Studio를 다시 시작합니다.

데이터 소스가 하나인 데이터베이스는 `NOTION_DATA_SOURCE_ID` 대신 `NOTION_DATABASE_ID`를 쓸 수 있습니다. 두 ID가 모두 설정되어 있으면 데이터 소스 ID가 우선합니다. 링크 전체나 뷰 ID 대신 해당 ID만 입력합니다.

앱을 실행할 때 Notion 데이터 소스의 속성 이름과 유형으로 표의 열을 만듭니다. 열 이름을 바꾸거나 속성을 추가한 뒤 앱을 다시 실행하면 반영됩니다. 제목 열은 맨 앞에 표시하고, 나머지는 스키마 응답 순서를 따릅니다. Notion의 개별 뷰에 저장된 열 순서와 표시 여부는 별도 설정입니다.

제목, 텍스트, 체크박스, 숫자, 선택, 다중 선택, 상태, 날짜, URL, 이메일, 전화번호를 표시합니다. 날짜 범위는 시작일을 표시하며, 값이 있는 미지원 유형은 안내 문구로 표시합니다.

표 위에 `Notion 일정 N건`이 표시되면 원본 데이터베이스와 몇 행을 비교합니다. 권한 오류가 표시되면 원본 데이터베이스에 Integration이 연결되어 있는지 확인합니다.

[Notion 인증 안내](https://developers.notion.com/guides/authorization/overview) · [데이터 소스 조회 명세](https://developers.notion.com/reference/query-a-data-source)

모든 소스와 문서는 UTF-8로 저장합니다.
