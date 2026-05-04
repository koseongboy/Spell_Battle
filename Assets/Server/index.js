const dns = require('node:dns');
dns.setServers(['8.8.8.8', '1.1.1.1']);

const express = require('express');
const mongoose = require('mongoose'); // DB 도구 불러오기
const app = express();
const PORT = 3000;

app.use(express.json());

// 1. DB 연결 (여기에 복사한 주소를 붙여넣으세요)
// <password> 부분은 아까 만든 비번으로 바꿔야 합니다.
const mongoURI = "mongodb+srv://kyungyi327:spellbattle4cap@cluster0.krtsii4.mongodb.net/?appName=Cluster0"; 
mongoose.connect(mongoURI)
    .then(() => console.log("✅ MongoDB 연결 성공!"))
    .catch(err => console.error("❌ DB 연결 실패:", err));

// 2. 데이터 구조(Schema) 정의 (유저 아이디, 점수, 골드)
const PlayerSchema = new mongoose.Schema({
    userId: String,
    score: Number,
    gold: Number,
    lastLogin: { type: Date, default: Date.now }
});
const Player = mongoose.model('Player', PlayerSchema);

// 3. 유니티에서 온 데이터 저장하기
app.post('/unity', async (req, res) => {
    try {
        const { userId, score, gold } = req.body;

        // DB에 저장하거나 업데이트 (이미 있으면 업데이트, 없으면 생성)
        const updatedPlayer = await Player.findOneAndUpdate(
            { userId: userId }, 
            { score: score, gold: gold, lastLogin: Date.now() },
            { upsert: true, new: true }
        );

        console.log("💾 DB 저장 완료:", updatedPlayer);
        res.json({ message: "DB 저장 성공!", data: updatedPlayer });
    } catch (err) {
        console.error("저장 에러:", err);
        res.status(500).json({ message: "서버 에러 발생" });
    }
});

app.listen(PORT, () => {
    console.log(`서버 가동 중: http://localhost:${PORT}`);
});