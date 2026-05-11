const dns = require('node:dns');
dns.setServers(['8.8.8.8', '1.1.1.1']);

const express = require('express');
const mongoose = require('mongoose');
const bcrypt = require('bcrypt');
const jwt = require('jsonwebtoken');
const app = express();
const PORT = 3000;

app.use(express.json());

const mongoURI = "mongodb+srv://kyungyi327:spellbattle4cap@cluster0.krtsii4.mongodb.net/?appName=Cluster0"; 
mongoose.connect(mongoURI)
    .then(() => console.log("✅ MongoDB 연결 성공!"))
    .catch(err => console.error("❌ DB 연결 실패:", err));

// --- 데이터 모델 정의 ---
const userSchema = new mongoose.Schema({
    userId: { type: String, required: true, unique: true },
    password: { type: String, required: true },
    score: { type: Number, default: 0 },
    rank: { type: Number, default: 0 }
});
const User = mongoose.model('User', userSchema);

// --- 1. 회원가입 (POST /register) ---
app.post('/register', async (req, res) => {
    try {
        const { userId, password } = req.body;
        const hashedPassword = await bcrypt.hash(password, 10); 
        const newUser = new User({ userId, password: hashedPassword });
        await newUser.save();
        res.status(201).json({ message: "회원가입 성공" });
    } catch (err) {
        res.status(400).send("회원가입 실패: " + err.message);
    }
});

// --- 2. 로그인 (POST /login) ---
app.post('/login', async (req, res) => {
    const { userId, password } = req.body;
    const user = await User.findOne({ userId });

    if (user && await bcrypt.compare(password, user.password)) {
        const token = jwt.sign({ id: user.userId }, 'YOUR_SECRET_KEY', { expiresIn: '2h' });
        res.json({ 
            success: true, 
            token: token, 
            score: user.score, 
            rank: user.rank
        });
    } else {
        res.status(401).json({ success: false, message: "아이디 또는 비밀번호 틀림" });
    }
});

// --- 3. 기존 유니티 데이터 저장 (POST /unity) ---
app.post('/unity', async (req, res) => {
    try {
        const { userId, score, rank } = req.body;
        const updatedPlayer = await User.findOneAndUpdate(
            { userId: userId }, 
            { score: score, rank: rank },
            { upsert: true, new: true }
        );
        console.log("💾 DB 저장 완료:", updatedPlayer);
        res.json({ message: "DB 저장 성공!", data: updatedPlayer });
    } catch (err) {
        res.status(500).json({ message: "서버 에러" });
    }
});

app.listen(PORT, () => {
    console.log(`서버 가동 중: http://localhost:${PORT}`);
});