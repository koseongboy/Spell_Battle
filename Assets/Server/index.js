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
const JWT_SECRET = process.env.JWT_SECRET || 'your_jwt_secret_key_here';

// 🤖 구글 제미나이 SDK 초기화
const ai = new GoogleGenAI({ apiKey: process.env.GEMINI_API_KEY });

// 🔌 Socket.io 서버 감싸기
const server = http.createServer(app);
const io = new Server(server, { cors: { origin: "*" } });

app.use(express.json());

// 🔊 오디오 파일 다운로드 및 재생을 위한 정적 폴더 개방
app.use('/uploads', express.static(path.join(__dirname, 'uploads')));

// 📁 오디오 파일 저장을 위한 Multer 설정
const storage = multer.diskStorage({
    destination: (req, file, cb) => {
        const uploadPath = path.join(__dirname, 'uploads');
        if (!fs.existsSync(uploadPath)) {
            fs.mkdirSync(uploadPath, { recursive: true });
        }
        cb(null, uploadPath);
    },
    filename: (req, file, cb) => {
        const uniqueSuffix = Date.now() + '-' + Math.round(Math.random() * 1E9);
        cb(null, file.fieldname + '-' + uniqueSuffix + path.extname(file.originalname));
    }
});
const upload = multer({ storage: storage });

// 🍃 MongoDB 연결
mongoose.connect(process.env.MONGODB_URI)
  .then(() => console.log('🍃 MongoDB 연결 성공'))
  .catch(err => console.error('❌ MongoDB 연결 실패:', err));

// ---------------------------------------------------------------- //
// 📑 데이터베이스 스키마 및 모델 정의
// ---------------------------------------------------------------- //

const userSchema = new mongoose.Schema({
    userId: { type: String, required: true, unique: true },
    password: { type: String, required: true },
    score: { type: Number, default: 0 },
    rank: { type: String, default: 'Bronze' },
    defaultPitch: { type: Number, default: 150.0 }
});
const User = mongoose.model('User', userSchema);

const deckSchema = new mongoose.Schema({
    deckName: { type: String, required: true },
    userId: { type: String, required: true }, 
    cards: { type: [String], required: true }, 
    description: { type: String, default: "" },
    createdAt: { type: Date, default: Date.now }
});
const Deck = mongoose.model('Deck', deckSchema);

// ---------------------------------------------------------------- //
// 🔐 1. 인증 및 유저 데이터 API
// ---------------------------------------------------------------- //

app.post('/register', async (req, res) => {
    try {
        const { userId, password } = req.body;
        if (!userId || !password) return res.status(400).json({ error: "아이디와 비밀번호를 입력해주세요." });

        const existingUser = await User.findOne({ userId });
        if (existingUser) return res.status(400).json({ error: "이미 존재하는 아이디입니다." });

        const hashedPassword = await bcrypt.hash(password, 10);
        const newUser = new User({ userId, password: hashedPassword });
        await newUser.save();

        res.status(201).json({ message: "회원가입 성공" });
    } catch (err) {
        res.status(500).json({ error: "회원가입 중 에러 발생" });
    }
});

app.post('/login', async (req, res) => {
    try {
        const { userId, password } = req.body;
        const user = await User.findOne({ userId });
        if (!user) return res.status(400).json({ error: "아이디 또는 비밀번호가 틀렸습니다." });

        const isMatch = await bcrypt.compare(password, user.password);
        if (!isMatch) return res.status(400).json({ error: "아이디 또는 비밀번호가 틀렸습니다." });

        const token = jwt.sign({ id: user._id, userId: user.userId }, JWT_SECRET, { expiresIn: '24h' });

        res.status(200).json({
            message: "로그인 성공",
            token,
            userData: {
                userId: user.userId,
                score: user.score,
                rank: user.rank,
                defaultPitch: user.defaultPitch
            }
        });
    } catch (err) {
        res.status(500).json({ error: "로그인 중 에러 발생" });
    }
});

app.get('/load/:userId', async (req, res) => {
    try {
        const { userId } = req.params;
        const user = await User.findOne({ userId });
        
        if (!user) {
            return res.status(404).json({ error: "존재하지 않는 유저아이디입니다." });
        }

        res.status(200).json({
            message: "유저 데이터 로드 성공",
            userData: {
                userId: user.userId,
                score: user.score,
                rank: user.rank,
                defaultPitch: user.defaultPitch
            }
        });
    } catch (err) {
        console.error("❌ 유저 데이터 로드 중 에러:", err);
        res.status(500).json({ error: "유저 데이터를 불러오는 중 서버 에러가 발생했습니다." });
    }
});

app.get('/default-pitch', async (req, res) => {
    try {
        const { userId } = req.query;
        if (!userId) return res.status(400).json({ error: "userId 쿼리 파라미터가 필요합니다." });

        const user = await User.findOne({ userId });
        if (!user) return res.status(404).json({ error: "유저를 찾을 수 없습니다." });

        res.status(200).json({ userId: user.userId, defaultPitch: user.defaultPitch });
    } catch (err) {
        res.status(500).json({ error: "서버 에러" });
    }
});

app.put('/default-pitch', async (req, res) => {
    try {
        const { userId, defaultPitch } = req.body;
        if (!userId || defaultPitch === undefined) {
            return res.status(400).json({ error: "userId와 defaultPitch 값이 필요합니다." });
        }

        const user = await User.findOneAndUpdate(
            { userId },
            { defaultPitch: Number(defaultPitch) },
            { new: true }
        );

        if (!user) {
            return res.status(404).json({ error: "유저를 찾을 수 없습니다." });
        }

        console.log(`🎵 [피치 업데이트 완료] 유저: ${userId} | 변경된 Pitch: ${user.defaultPitch}`);
        res.status(200).json({
            message: "디폴트 피치가 성공적으로 수정되었습니다.",
            userId: user.userId,
            defaultPitch: user.defaultPitch
        });
    } catch (err) {
        console.error("❌ 디폴트 피치 수정 중 에러:", err);
        res.status(500).json({ error: "디폴트 피치를 수정하는 중 서버 에러가 발생했습니다." });
    }
});

// ---------------------------------------------------------------- //
// 🎴 2. 공유 덱 관련 API
// ---------------------------------------------------------------- //

app.post('/decks', async (req, res) => {
    try {
        const { deckName, userId, cards, description } = req.body;
        if (!deckName || !userId || !cards || cards.length === 0) {
            return res.status(400).json({ error: "덱 이름, 유저 ID, 카드가 누락되었습니다." });
        }

        const newDeck = new Deck({ deckName, userId, cards, description });
        await newDeck.save();

        res.status(201).json({ message: "공유 덱이 등록되었습니다.", deckId: newDeck._id });
    } catch (err) {
        res.status(500).json({ error: "덱 저장 중 서버 에러 발생" });
    }
});

app.get('/decks', async (req, res) => {
    try {
        const decks = await Deck.find().sort({ createdAt: -1 });
        res.status(200).json(decks);
    } catch (err) {
        res.status(500).json({ error: "덱 목록 로드 중 서버 에러 발생" });
    }
});

app.delete('/decks/:deckId', async (req, res) => {
    try {
        const { deckId } = req.params;
        const { userId } = req.body; 

        if (!userId) {
            return res.status(400).json({ error: "유저 ID(userId) 검증 데이터가 필요합니다." });
        }

        const deck = await Deck.findById(deckId);
        if (!deck) {
            return res.status(404).json({ error: "삭제하려는 덱을 찾을 수 없습니다." });
        }

        if (deck.userId !== userId) {
            return res.status(403).json({ error: "본인이 작성한 덱만 삭제할 수 있습니다." });
        }

        await Deck.findByIdAndDelete(deckId);
        console.log(`🗑️ [덱 삭제 완료] 덱 ID: ${deckId} | 소유자: ${userId}`);
        res.status(200).json({ message: "공유 덱이 성공적으로 삭제되었습니다." });
    } catch (err) {
        console.error("❌ 덱 삭제 중 서버 에러:", err);
        res.status(500).json({ error: "서버 내부 에러로 인해 덱을 삭제하지 못했습니다." });
    }
});

// ---------------------------------------------------------------- //
// 🔊 3. 비동기 음성 인식 및 채점 관련 로직
// ---------------------------------------------------------------- //

const tasks = {}; // 메모리 기반 비동기 태스크 상태 관리 객체

// 백그라운드 AI 채점 연동 함수
async function runBackgroundAnalysis(taskId, filePath, concept, prefix, wordNames, defaultPitch) {
    try {
        // 💡 수정: FastAPI 규격에 맞게 공백이 아닌 '쉼표(,)'로 단어 배열을 결합합니다.
        const targetWordsString = Array.isArray(wordNames) ? wordNames.join(',') : wordNames;

        const formData = new FormData();
        formData.append('file', fs.createReadStream(filePath));
        formData.append('default_pitch', defaultPitch.toString());
        formData.append('target_words', targetWordsString); 

        // 1. FastAPI (AI 분석 전용 서버 5000포트) 연동
        const pythonResponse = await axios.post('http://localhost:5000/analyze-audio', formData, {
            headers: formData.getHeaders()
        });

        const { text_validation, emotions, audio_features } = pythonResponse.data;
        const pitch_ratio = audio_features?.pitch_ratio || 1.0;
        const speed_ratio = audio_features?.speed_ratio || 1.0;
        const stt_text = text_validation?.recognized_text || "";
        const is_matched = text_validation?.is_matched || false;

        // 2. Gemini LLM용 채점 프롬프트 작성
        const prompt = `
        유저가 게임 속 캐릭터 컨셉 '${concept}'에 맞추어 마법 영창(대사)을 외쳤습니다. 
        아래 음성 분석 데이터를 정밀 분석하여 최종 연기력 점수를 산출해 주세요.

        [분석 데이터]
        - 유저 캐릭터 컨셉: ${concept}
        - 대사 접두어(Prefix): ${prefix || "없음"}
        - 목표 영창 카드 목록(Word Names): ${targetWordsString}
        - 실제 인식된 영창 대본(STT): ${stt_text}
        - 필수 단어 일치 여부: ${is_matched ? "일치함" : "누락되거나 다름"}
        - 기준점 대비 음높이 배율(pitch_ratio): ${pitch_ratio} (1.0 기준, 높을수록 하이톤)
        - 기준점 대비 발음 속도 배율(speed_ratio): ${speed_ratio} (1.0 기준, 높을수록 빠름)

        [컨셉별 채점 가이드라인]
        - 매드 사이언티스트 / 미치광이 광대 / 마법소녀: pitch_ratio가 하이톤(1.15 이상)이거나 감정 중 'happy/angry' 가 두드러지면 고득점.
        - 우직한 문지기 / 성을 지키는 용 / 덩치 큰 근육맨: pitch_ratio가 로우톤(0.85 이하)이거나 낮고 느리게 읊조리면 고득점.
        - 세상을 지배하는 로봇: 속도 배율(speed_ratio)과 톤 변동이 일정할수록 고득점.

        [⚠️주의사항] '필수 단어 일치 여부'가 거짓(False)이거나 대본 인식 상태가 너무 불량하면 과감하게 감점 처리하세요.

        [출력 포맷] 반드시 JSON 객체 단 하나만 반환하세요. 공백 및 마크다운 텍스트(예: \`\`\`json)는 절대 포함하지 마십시오.
        {
          "score": (1부터 100 사이의 정수),
          "reason": "점수를 책정한 근거 요약문"
        }
        `;

        // 3. Gemini Content API 구동
        const response = await ai.models.generateContent({
            model: 'gemini-2.5-flash',
            contents: prompt,
            config: { responseMimeType: 'application/json' }
        });

        const resultJson = JSON.parse(response.text.trim());

        // 4. 전역 태스크 상태 완료 업데이트
        tasks[taskId] = {
            status: "completed",
            score: resultJson.score,
            reason: resultJson.reason, 
            updatedAt: Date.now()
        };
        console.log(`🗑️ [태스크 완료] ID: ${taskId} | 점수: ${resultJson.score}점`);

    } catch (err) {
        console.error(`❌ [태스크 오류] ID: ${taskId} 처리 중 예외 발생:`, err.message);
        tasks[taskId] = { status: "failed", error: err.message, updatedAt: Date.now() };
    } finally {
        if (fs.existsSync(filePath)) {
            fs.unlink(filePath, (err) => { if (err) console.error("임시 파일 삭제 실패:", err); });
        }
    }
}

// [POST] /upload-voice-async
app.post('/upload-voice-async', upload.single('audio'), async (req, res) => {
    try {
        if (!req.file) return res.status(400).json({ error: "음성 파일(audio)이 전송되지 않았습니다." });
        if (!req.body.metadata) return res.status(400).json({ error: "메타데이터(metadata)가 누락되었습니다." });

        const metadata = JSON.parse(req.body.metadata);
        const { userId, concept, prefix, wordNames } = metadata; 

        if (!userId || !concept || !wordNames) {
            return res.status(400).json({ error: "메타데이터 세부 정보(userId, concept, wordNames)가 부족합니다." });
        }

        const user = await User.findOne({ userId });
        const defaultPitch = user ? user.defaultPitch : 150.0;

        const taskId = `task_${Date.now()}_${Math.random().toString(36).substr(2, 5)}`;
        const audioUrl = `${req.protocol}://${req.get('host')}/uploads/${req.file.filename}`;

        tasks[taskId] = { status: "processing", audioUrl, updatedAt: Date.now() };

        runBackgroundAnalysis(taskId, req.file.path, concept, prefix, wordNames, defaultPitch);

        res.status(200).json({ taskId, audioUrl });

    } catch (err) {
        console.error("음성 업로드 실패:", err);
        res.status(500).json({ error: "음성 비동기 처리 도중 에러가 발생했습니다." });
    }
});

// 💡 수정: 라우터 주소 스펙을 이미지 양식에 맞춰 Query Parameter 방식으로 전면 수정
// [GET] /evaluation-result?taskId={taskId}
app.get('/evaluation-result', (req, res) => {
    const { taskId } = req.query; // 👈 params가 아닌 query에서 추출

    if (!taskId) {
        return res.status(400).json({ error: "taskId 쿼리 파라미터가 누락되었습니다." });
    }

    const task = tasks[taskId];

    if (!task) {
        return res.status(404).json({ error: "요청하신 유효한 태스크 카드를 찾을 수 없습니다." });
    }

    if (task.status === "processing") {
        return res.status(200).json({ 
            status: "processing", 
            message: "아직 평가 중입니다." 
        });
    }

    if (task.status === "failed") {
        return res.status(500).json({ 
            status: "failed", 
            message: "평가 실패",
            error: task.error 
        });
    }

    res.status(200).json({
        status: "completed",
        message: `평가 완료: ${task.score}점`,
        score: task.score,
        reason: task.reason
    });
});

// ---------------------------------------------------------------- //
// 🔌 4. 웹소켓 (Socket.io) P2P 방 관리 및 매칭 통신
// ---------------------------------------------------------------- //
const rooms = {}; 

io.on('connection', (socket) => {
    console.log(`🔌 유저 커넥션 연동 완료: ${socket.id}`);

    socket.on('joinRoom', ({ roomId, userId }) => {
        socket.join(roomId);
        socket.userId = userId;
        socket.roomId = roomId;

        if (!rooms[roomId]) {
            rooms[roomId] = { roomId, players: [], status: "waiting", currentTurn: "" };
        }

        if (!rooms[roomId].players.find(p => p.userId === userId)) {
            rooms[roomId].players.push({ userId, socketId: socket.id, isReady: false });
        }

        console.log(`🏠 [방입장] 유저 ${userId} -> 방 ${roomId}`);
        io.to(roomId).emit('roomUpdate', rooms[roomId]);
    });

    socket.on('castSpell', ({ cardName, audioUrl, score }) => {
        const roomId = socket.roomId;
        socket.to(roomId).emit('opponentCast', {
            userId: socket.userId,
            cardName,
            audioUrl,
            score
        });
    });

    socket.on('disconnect', () => {
        console.log(`❌ 유저 커넥션 해제: ${socket.id}`);
        const roomId = socket.roomId;
        if (roomId && rooms[roomId]) {
            rooms[roomId].players = rooms[roomId].players.filter(p => p.socketId !== socket.id);
            io.to(roomId).emit('roomUpdate', rooms[roomId]);
        }
    });
});

server.listen(PORT, () => {
    console.log(`🚀 [서버 개방 완료] 메인 게임 백엔드가 포트 ${PORT}에서 작동 중입니다.`);
});