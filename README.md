# 🎙️ Spell Battle (마법 영창 기반 PVP 전략 CCG)

> **2026-1학기 소프트웨어학부 캡스톤디자인 05분반 프로젝트**
> 
> 자신이 직접 마법 주문을 조합하고, 목소리와 감정을 담아 영창하여 상대와 대결하는 신개념 하이브리드 음성 인식 카드 게임입니다.

<br>

## 🎮 게임 개요
기존의 단조로운 대사 낭독형 음성 인식 게임에서 벗어나, 플레이어가 주도적으로 주문을 조립하고 롤플레잉(TRPG) 하듯 감정을 담아 발화하는 **능동적 음성 액션**과 1대1 **TCG/CCG의 전략성**을 결합한 프로토타입 게임입니다. 

- **장르:** 마법 영창 PVP 전략 카드 게임 (CCG)
- **플랫폼:** PC (Windows Standalone)
- **타겟 유저:** 과몰입 롤플레잉을 즐기는 서브컬처 게이머, 파티 게임 유저, 인터넷 방송 크리에이터

<br>

## ✨ 핵심 기능 (Core Features)

1. **모듈형 단어 조합 시스템** 
   - 지정된 대본이 아닌, 수식어와 마법 단어(모듈)를 조합해 자신만의 고유한 영창 텍스트 생성.
2. **AI 하이브리드 음성/감정 판정 파이프라인** 
   - 플레이어의 발화 뉘앙스, 주파수(Pitch/Speed), 텍스트의 캐릭터 컨셉 적합성을 종합 분석하여 1~100점의 연기력 점수 부여.
   - 점수에 따라 3단계(실패/성공/대성공)로 스킬 위력 및 시각적 이펙트(VFX) 차등 적용.
3. **실시간 1v1 PVP 전투 및 덱 빌딩** 
   - CSV 및 Scriptable Object 기반의 기획 데이터 연동.
   - 플레이어 간 실시간 P2P 네트워크 매칭 및 턴제 전투 로직 동기화.

<br>

## 🛠 기술 스택 (Tech Stack)

### Client
- **Engine:** Unity 6
- **UI:** UI Toolkit (UXML Viewport)
- **Architecture:** MVVM, MVP, Singleton, Factory, Command Pattern
- **Optimization:** Addressables, ObjectPool

### Backend & Network
- **Main Server:** Node.js, Express, MongoDB
- **Real-time Network:** Socket.io, Unity NetworkManager (P2P)

### AI & Audio Analysis
- **Framework:** Python, FastAPI
- **Audio Processing:** librosa
- **AI Models:** Whisper (STT), Wav2Vec2 (Emotion/Pitch), Google Gemini 2.5 Flash LLM

<br>

## 🏗 시스템 아키텍처

- 클라이언트의 연산 부하를 줄이기 위해 AI 분석을 마이크로서비스 형태의 Python 서버로 분리하여 비동기 처리합니다.
- TCG 전투 상태 데이터는 Socket.io 기반의 실시간 동기화를 거칩니다.

<br>

## 👥 팀원 및 역할 (Contributors)

| 이름 | 역할 및 담당 업무 |
|:---:|---|
| **고성현** | **Client Architecture & Core Battle Logic:** Unity 클라이언트 아키텍처 설계, 커맨드 패턴 기반 턴(Turn) 흐름 및 인게임 코어 전투 로직 구현, 네트워크 P2P 동기화 로직 적용, 스킬 VFX 연출 |
| **김명준** | **Game Design & UI/UX:** 핵심 아이디어 기획 및 TCG 카드 밸런스 데이터 파이프라인(CSV/Scriptable Object) 설계, UI Toolkit 기반 반응형 화면 구축, 에셋 가공 |
| **김이경** | **AI Pipeline & Backend:** Node.js 메인 서버 및 Python(FastAPI) 분석 서버 구축, Whisper/Wav2Vec2 라이브러리 연동, Gemini 2.5 Flash 프롬프트 엔지니어링 및 음성 채점 API 구축 |

<br>

## 🚀 실행 방법 (Getting Started)

### 1. 사전 요구 사항 (Prerequisites)
- 마이크 입력 장치 필수 (16kHz 이상 오디오 샘플링 권장)
- Node.js (v18+) 및 Python 3.10+ 설치 환경

### 2. 클라이언트 실행
Unity Hub에서 프로젝트를 열거나, Release 탭에서 최신 Windows 빌드(.exe)를 다운로드하여 실행합니다.

인게임 매뉴얼 및 덱 구성 가이드는 프로젝트 폴더 내 [Spell_Battle_Manual.pdf]를 참고해 주세요.
