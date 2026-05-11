const express = require('express');
const mongoose = require('mongoose');
const bcrypt = require('bcrypt'); // 비밀번호 암호화용 [cite: 16, 20]
const jwt = require('jsonwebtoken'); // 인증 토큰용 [cite: 16, 20]
const app = express();

app.use(express.json()); // JSON 데이터 파싱용

// 1. 유저 모델 정의 (새 파일로 분리 가능)
const userSchema = new mongoose.Schema({
    userId: { type: String, required: true, unique: true },
    password: { type: String, required: true },
    score: { type: Number, default: 0 },
    rank: { type: Number, default: 0 }
});
const User = mongoose.model('User', userSchema);

// 2. 회원가입 (POST /register)
app.post('/register', async (req, res) => {
    try {
        const { userId, password } = req.body;
        // 비밀번호 암호화 (Salt 횟수 10번) 
        const hashedPassword = await bcrypt.hash(password, 10); 
        const newUser = new User({ userId, password: hashedPassword });
        await newUser.save();
        res.status(201).json({ message: "회원가입 성공" });
    } catch (err) {
        res.status(400).send("회원가입 실패: " + err.message);
    }
});

// 3. 로그인 및 데이터 로드 (POST /login)
app.post('/login', async (req, res) => {
    const { userId, password } = req.body;
    const user = await User.findOne({ userId });

    if (user && await bcrypt.compare(password, user.password)) {
        // 비밀번호 일치 시 JWT 토큰 생성 [cite: 24]
        const token = jwt.sign({ id: user.userId }, 'YOUR_SECRET_KEY', { expiresIn: '2h' });
        // 로그인 성공 시 점수와 골드 데이터를 함께 반환 [cite: 21]
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