const dns = require('node:dns');
dns.setServers(['8.8.8.8', '1.1.1.1']);

require('dotenv').config();

const express = require('express');
const http = require('http');
const { Server } = require('socket.io'); 
const mongoose = require('mongoose');
const bcrypt = require('bcrypt');
const jwt = require('jsonwebtoken');
const multer = require('multer');
const axios = require('axios');
const FormData = require('form-data');
const fs = require('fs');
const path = require('path');
const { GoogleGenAI, Type } = require('@google/genai');

const app = express();
const PORT = process.env.PORT || 3000;

// 발급받은 Gemini API 키 기반 객체 초기화
const ai = new GoogleGenAI({ apiKey: process.env.GEMINI_API_KEY });

// --- MongoDB 연결 및 스키마 설정 ---
mongoose.connect(process.env.MONGODB_URI)
  .then(() => console.log('🍃 MongoDB 연결 성공'))
  .catch(err => console.error('❌ MongoDB 연결 실패:', err));

const userSchema = new mongoose.Schema({
    userId: { type: String, required: true, unique: true },
    password: { type: String, required: true },
    score: { type: Number, default: 0 },
    rank: { type: String, default: 'Bronze' },
    defaultPitch: { type: Number, default: 150.0 }
});
const User = mongoose.model('User', userSchema);

const deckSchema = new mongoose.Schema({
    userId: { type: String, required: true },
    deckName: { type: String, required: true },
    cards: { type: [String], required: true },
    createdAt: { type: Date, default: Date.now }
});
const Deck = mongoose.model('Deck', deckSchema);

// --- 캐릭터 음성 가이드 가이드라인 ---
const CHARACTER_VOICE_GUIDES = {
    "소년만화주인공": { desc: "힘차고 열정적인 주인공 톤", targetPitch: "pitch_ratio 1.1 ~ 1.3", expectedEmotions: "happy 또는 surprise" },
    "매드사이언티스트": { desc: "날카롭고 미치광이 같은 하이톤 연기", targetPitch: "pitch_ratio 1.3 이상", expectedEmotions: "angry, fear, surprise" },
    "미치광이광대": { desc: "높고 낮음이 기괴하게 변하는 조커 스타일 톤", targetPitch: "pitch_ratio 1.3 이상", expectedEmotions: "happy(광기) 또는 surprise" },
    "우직한문지기": { desc: "듬직하고 톤이 일정하며 느릿한 목소리", targetPitch: "pitch_ratio 0.85 ~ 0.95", expectedEmotions: "neutral 지배적" },
    "성을지키는용": { desc: "낮고 으르렁거리는 거대하고 무거운 괴수 톤", targetPitch: "pitch_ratio 0.8 이하 필수", expectedEmotions: "angry 압도적" },
    "세상을지배하는로봇": { desc: "감정이 전혀 느껴지지 않는 완벽한 기계식 리딩", targetPitch: "pitch_ratio 0.95 ~ 1.05 유지", expectedEmotions: "neutral 90% 이상" },
    "마법소녀": { desc: "사랑스럽고 활기찬 초고음 하이톤 연기", targetPitch: "pitch_ratio 1.35 이상", expectedEmotions: "happy 또는 surprise" },
    "덩치큰근육맨": { desc: "굵고 힘이 꽉 들어간 마초 스타일 목소리", targetPitch: "pitch_ratio 0.8 ~ 0.9", expectedEmotions: "angry 또는 neutral" },
    "정의의기사": { desc: "단호하고 당당하며 템포가 빠른 영웅의 목소리", targetPitch: "pitch_ratio 1.05 ~ 1.2", expectedEmotions: "happy 또는 neutral" },
    "늑대인간": { desc: "야수성이 드러나는 거칠고 위협적인 목소리", targetPitch: "pitch_ratio 0.85 이하", expectedEmotions: "angry 또는 fear" },
    "쪼그만외계인": { desc: "익살스럽고 아주 빠르며 가벼운 고음 목소리", targetPitch: "pitch_ratio 1.4 이상", expectedEmotions: "surprise 또는 happy" },
    "인자한제갈량": { desc: "모든 것을 통달한 듯 여유롭고 부드러우며 느린 목소리", targetPitch: "pitch_ratio 0.9 ~ 1.0", expectedEmotions: "neutral 완벽 유지" },
    "능글맞은두꺼비": { desc: "장난기 가득하고 속을 알 수 없는 변칙적인 톤", targetPitch: "pitch_ratio 0.85 ~ 1.0", expectedEmotions: "happy 또는 neutral" }
};

// --- 비동기 태스크 저장을 위한 인메모리 객체 ---
const tasks = {};

// --- HTTP 및 Socket.io 웹서버 설정 ---
const server = http.createServer(app);
const io = new Server(server, { cors: { origin: "*" } });

app.use(express.json());
if (!fs.existsSync('uploads')) { fs.mkdirSync('uploads'); }
app.use('/uploads', express.static(path.join(__dirname, 'uploads')));

const storage = multer.diskStorage({
    destination: (req, file, cb) => cb(null, 'uploads/'),
    filename: (req, file, cb) => cb(null, `${Date.now()}-${file.originalname}`)
});
const upload = multer({ storage });

// --- [기존 라우트] 회원가입, 로그인, 디폴트 피치 설정, 덱 공유 기능 ---
app.post('/register', async (req, res) => {
    try {
        const { userId, password } = req.body;
        const hashedPassword = await bcrypt.hash(password, 10);
        const newUser = new User({ userId, password: hashedPassword });
        await newUser.save();
        res.status(201).json({ message: "회원가입 성공" });
    } catch (err) { res.status(500).json({ error: err.message }); }
});

app.post('/login', async (req, res) => {
    try {
        const { userId, password } = req.body;
        const user = await User.findOne({ userId });
        if (!user || !(await bcrypt.compare(password, user.password))) {
            return res.status(401).json({ error: "아이디 또는 비밀번호가 틀렸습니다." });
        }
        const token = jwt.sign({ userId: user.userId }, 'SECRET_KEY');
        res.json({ token, userId: user.userId, score: user.score, rank: user.rank, defaultPitch: user.defaultPitch });
    } catch (err) { res.status(500).json({ error: err.message }); }
});

app.post('/set-default-pitch', async (req, res) => {
    try {
        const { userId, defaultPitch } = req.body;
        await User.findOneAndUpdate({ userId }, { defaultPitch });
        res.json({ message: "디폴트 피치 세팅 저장 완료" });
    } catch (err) { res.status(500).json({ error: err.message }); }
});

app.post('/decks', async (req, res) => {
    try {
        const { userId, deckName, cards } = req.body;
        const newDeck = new Deck({ userId, deckName, cards });
        await newDeck.save();
        res.status(201).json({ message: "덱이 성공적으로 공유되었습니다!" });
    } catch (err) { res.status(500).json({ error: err.message }); }
});

app.get('/decks', async (req, res) => {
    try {
        const decks = await Deck.find().sort({ createdAt: -1 });
        res.json(decks);
    } catch (err) { res.status(500).json({ error: err.message }); }
});


/**
 * 1. [POST] /upload-voice-async
 * 다이어그램의 "C1 ->> WS: [POST] 음성 파일 & JSON 제출" 단계 처리
 */
app.post('/upload-voice-async', upload.single('audio'), async (req, res) => {
    try {
        if (!req.file) return res.status(400).json({ error: "음성 파일이 없습니다." });

        // 메타데이터 파싱 예외 처리 통합
        let metadata = req.body;
        if (typeof req.body.metadata === 'string') {
            metadata = JSON.parse(req.body.metadata);
        } else if (req.body.metadata) {
            metadata = req.body.metadata;
        }
        
        const { userId, characterType, script } = metadata;

        const taskId = `task_${Date.now()}_${Math.random().toString(36).substr(2, 5)}`;
        const audioUrl = `${req.protocol}://${req.get('host')}/uploads/${req.file.filename}`;

        // 태스크의 초기 상태 등록
        tasks[taskId] = {
            status: "processing",
            score: null,
            reason: null,
            createdAt: Date.now()
        };

        // 클라이언트(C1)에게 즉시 대기표 및 URL 반환 (Non-blocking)
        res.json({ taskId, audioUrl });

        // 백그라운드에서 AI 분석 연동 수행
        runBackgroundAnalysis(taskId, req.file.path, userId, characterType, script);

    } catch (err) {
        console.error("파일 업로드 에러:", err);
        res.status(500).json({ error: err.message });
    }
});

/**
 * 2. [GET] /tasks/:taskId
 * ★ 버그 수정: 매개변수 순서 정정 (res, req) -> (req, res) ★
 * 다이어그램의 "loop 점수 확인 (Polling)" 단계 처리
 */
app.get('/tasks/:taskId', (req, res) => {
    const { taskId } = req.params;
    const task = tasks[taskId];

    if (!task) {
        return res.status(404).json({ error: "존재하지 않는 Task ID입니다." });
    }

    if (task.status === "processing") {
        return res.json({ status: "processing", message: "아직 평가 중" });
    }

    if (task.status === "failed") {
        return res.status(500).json({ status: "failed", error: task.error });
    }

    // 평가 완료 상태 응답 반환
    res.json({
        status: "completed",
        message: `평가 완료: ${task.score}점`,
        score: task.score,
        reason: task.reason
    });
});

/**
 * 백그라운드 분석 및 Gemini LLM 채점 처리 함수
 */
async function runBackgroundAnalysis(taskId, filePath, userId, characterType, script) {
    try {
        // 1. 유저의 디폴트 피치 데이터 조회
        const user = await User.findOne({ userId });
        const defaultPitch = user ? user.defaultPitch : 150.0;

        // 2. FastAPI 음성 분석 서버 연동 준비
        const formData = new FormData();
        formData.append('file', fs.createReadStream(filePath));
        formData.append('default_pitch', defaultPitch.toString());
        formData.append('target_text', script || ""); // ★ 버그 수정: 누락되었던 target_text 매개변수 추가

        const pythonResponse = await axios.post('http://localhost:5000/analyze-audio', formData, {
            headers: formData.getHeaders()
        });

        // ★ 버그 수정: FastAPI 반환 JSON 트리 계층 구조 정정 및 매칭 ★
        const { text_validation, emotions, audio_features } = pythonResponse.data;
        const { recognized_text, is_matched } = text_validation;
        const { current_pitch, pitch_ratio, duration_seconds } = audio_features;

        // 템포 검증용 변수 (기본 1.0배율 설정)
        const speed_ratio = audio_features.speed_ratio || 1.0; 

        const guide = CHARACTER_VOICE_GUIDES[characterType] || { desc: "일반 리딩", targetPitch: "기본", expectedEmotions: "없음" };

        // 3. Gemini LLM 채점 처리
        const prompt = `
            [영창 평가 시스템]
            - 유저가 외친 대사(STT): "${recognized_text}"
            - 원래 외쳐야 했을 정답 지시문: "${script}"
            - 지시문 매칭 성공 여부: ${is_matched ? "성공" : "실패"}
            - 선택한 캐릭터 컨셉: "${characterType}" (${guide.desc})
            - 요구되는 음성 피치 조건: ${guide.targetPitch}, 감정 조건: ${guide.expectedEmotions}
            
            [실제 분석된 유저의 물리 데이터]
            - 피치 비율(기준점 대비): ${pitch_ratio}배 (현재 피치: ${current_pitch}Hz)
            - 속도 비율(기준점 대비): ${speed_ratio}배 (발성 시간: ${duration_seconds}초)
            - 감정 분석 결과: ${JSON.stringify(emotions)}

            위 데이터를 종합해서 발음의 정확도(지시문 매칭률), 캐릭터 연기력(피치 및 감정 조건 부합도)을 고려해 1~100점 사이로 채점해 주세요.
            지시문 매칭이 '실패'했거나 발음이 너무 다르면 점수를 크게 감점해야 합니다.
            반드시 아래 JSON 포맷으로만 답변하세요. 다른 설명은 절대 금지합니다.
            { "score": 점수(숫자), "reason": "채점 이유 요약" }
        `;

        const response = await ai.models.generateContent({
            model: 'gemini-2.5-flash',
            contents: prompt,
            config: { responseMimeType: 'application/json' }
        });

        const resultJson = JSON.parse(response.text.trim());

        // 4. 전역 태스크 객체 결과 업데이트
        tasks[taskId] = {
            status: "completed",
            score: resultJson.score,
            reason: resultJson.reason,
            updatedAt: Date.now()
        };

        console.log(`[태스크 완료] ID: ${taskId} | 점수: ${resultJson.score}점`);

    } catch (err) {
        console.error(`[태스크 오류] ID: ${taskId} 처리 중 에러:`, err);
        tasks[taskId] = {
            status: "failed",
            error: err.message,
            updatedAt: Date.now()
        };
    }
}

// --- 소켓을 이용한 기본 방 정보 연동 ---
const rooms = {};
io.on('connection', (socket) => {
    socket.on('joinRoom', ({ roomId, userId }) => {
        socket.join(roomId);
        if (!rooms[roomId]) rooms[roomId] = { id: roomId, players: [] };
        if (!rooms[roomId].players.includes(userId)) rooms[roomId].players.push(userId);
        io.to(roomId).emit('roomUpdate', rooms[roomId]);
    });
});

server.listen(PORT, () => {
    console.log(`🚀 시퀀스 대응 통합 서버가 포트 ${PORT}에서 작동 중입니다.`);
});