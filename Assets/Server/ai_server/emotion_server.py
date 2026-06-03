from fastapi import FastAPI, UploadFile, File, Form
from transformers import pipeline
import librosa
import numpy as np
import soundfile as sf
import io

app = FastAPI()

# 1. Wav2Vec2 감정 분석 파이프라인 로드
emotion_classifier = pipeline("audio-classification", model="Dpngtm/wav2vec2-emotion-recognition")

# 2. Whisper STT(음성 인식) 파이프라인 추가 (단어 체크용)
stt_pipeline = pipeline("automatic-speech-recognition", model="openai/whisper-tiny") # 가벼운 tiny 모델 권장

@app.post("/analyze-audio")
async def analyze_audio(
    file: UploadFile = File(...), 
    default_pitch: float = Form(150.0),
    target_text: str = Form("") # 유니티나 메인 서버에서 검증하고 싶은 '지시문/덱 단어'를 넘겨받음
):
    # 파일 읽기
    audio_bytes = await file.read()
    data, samplerate = sf.read(io.BytesIO(audio_bytes))
    
    # 16kHz 리샘플링 (Wav2Vec2 및 Whisper 최적화 사양)
    if samplerate != 16000:
        data = librosa.resample(data, orig_sr=samplerate, target_sr=16000)
        samplerate = 16000

    # 파일 임시 저장
    temp_filename = "temp.wav"
    sf.write(temp_filename, data, samplerate)
    
    # --- 1. 음성 인식 (STT) 수행 ---
    stt_result = stt_pipeline(temp_filename)
    recognized_text = stt_result.get("text", "").strip() # 유저가 실제로 말한 텍스트
    
    # --- 2. 덱 단어 / 지시문 포함 여부 검사 ---
    is_word_matched = False
    if target_text:
        # 공백을 없애고 소문자로 변환하여 유연하게 매칭 (영어/한국어 공용 팁)
        clean_target = target_text.replace(" ", "").lower()
        clean_recognized = recognized_text.replace(" ", "").lower()
        if clean_target in clean_recognized:
            is_word_matched = True

    # 3. 이모션 디텍터 구동
    emotion_results = emotion_classifier(temp_filename)
    
    # 4. 물리적 피처 분석 (기본 Pitch 계산)
    pitches, magnitudes = librosa.piptrack(y=data, sr=samplerate)
    pitch_values = pitches[pitches > 0]
    current_pitch = float(np.mean(pitch_values)) if len(pitch_values) > 0 else 120.0
    
    pitch_ratio = current_pitch / default_pitch
    duration = librosa.get_duration(y=data, sr=samplerate)
    
    return {
        "text_validation": {
            "recognized_text": recognized_text,      # 서버가 인식한 실제 말소리
            "target_text": target_text,              # 맞춰야 했던 지시문/단어
            "is_matched": is_word_matched            # 통과 여부 (True / False)
        },
        "emotions": {res['label']: round(res['score'] * 100, 1) for res in emotion_results},
        "audio_features": {
            "current_pitch": round(current_pitch, 1),
            "pitch_ratio": round(pitch_ratio, 2),
            "duration_seconds": round(duration, 2)
        }
    }

if __name__ == "__main__":
    import uvicorn
    uvicorn.run("emotion_server:app", host="127.0.0.1", port=5000, reload=True)