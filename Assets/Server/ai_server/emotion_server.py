from fastapi import FastAPI, UploadFile, File, Form
from transformers import pipeline
import librosa
import numpy as np
import soundfile as sf
import io
import os

app = FastAPI()

# 1. Wav2Vec2 감정 분석 파이프라인 로드
emotion_classifier = pipeline("audio-classification", model="Dpngtm/wav2vec2-emotion-recognition")

# 2. Whisper STT(음성 인식) 파이프라인 추가 (단어 체크 및 대본 리턴용)
stt_pipeline = pipeline("automatic-speech-recognition", model="openai/whisper-tiny")

@app.post("/analyze-audio")
async def analyze_audio(
    file: UploadFile = File(...), 
    default_pitch: float = Form(150.0),
    target_words: str = Form("") # 💡 변경: "파이어볼,나와라,공격" 처럼 쉼표로 구분된 단어 리스트를 받음
):
    # 파일 읽기
    audio_bytes = await file.read()
    data, samplerate = sf.read(io.BytesIO(audio_bytes))
    
    # 16kHz 리샘플링 (Wav2Vec2 및 Whisper 최적화 사양)
    if samplerate != 16000:
        data = librosa.resample(data, orig_sr=samplerate, target_sr=16000)
        samplerate = 16000

    # 파일 임시 저장 (동시 요청 대비를 위해 프로세스 ID나 고유값 권장하지만 우선 기존대로 유지)
    temp_filename = "temp.wav"
    sf.write(temp_filename, data, samplerate)
    
    # --- 1. 음성 인식 (STT) 수행 및 영창 대본 추출 ---
    stt_result = stt_pipeline(temp_filename)
    recognized_text = stt_result.get("text", "").strip() # 🌟 유저가 실제로 말한 전체 대본 (반드시 리턴)
    
    # --- 2. 💡 다중 필수 단어 검증 로직으로 업그레이드 ---
    is_all_matched = False
    word_check_results = {} # 각 단어별 매칭 여부를 상세히 기록할 딕셔너리
    
    if target_words:
        # 공백 제거 및 소문자화된 인식 대본 준비
        clean_recognized = recognized_text.replace(" ", "").lower()
        
        # 쉼표로 분리하여 검증할 단어 배열 생성 (예: ["파이어볼", "나와라"])
        words_list = [w.strip() for w in target_words.split(",") if w.strip()]
        
        matched_count = 0
        for word in words_list:
            clean_word = word.replace(" ", "").lower()
            if clean_word in clean_recognized:
                word_check_results[word] = True
                matched_count += 1
            else:
                word_check_results[word] = False
                
        # 필수 지정 단어가 모두 포함되었을 때만 최종 통과(True) 처리
        if len(words_list) > 0 and matched_count == len(words_list):
            is_all_matched = True
    else:
        # 타겟 단어가 들어오지 않았다면 검증 패스 (True로 처리하거나 기획에 맞춤)
        is_all_matched = True

    # 3. 이모션 디텍터 구동
    emotion_results = emotion_classifier(temp_filename)
    
    # 4. 물리적 피처 분석 (기본 Pitch 계산)
    pitches, magnitudes = librosa.piptrack(y=data, sr=samplerate)
    pitch_values = pitches[pitches > 0]
    current_pitch = float(np.mean(pitch_values)) if len(pitch_values) > 0 else 120.0
    
    pitch_ratio = current_pitch / default_pitch
    duration = librosa.get_duration(y=data, sr=samplerate)
    
    # 임시 파일 삭제 (서버 용량 관리용)
    if os.path.exists(temp_filename):
        os.remove(temp_filename)
    
    return {
        "text_validation": {
            "recognized_text": recognized_text,      # 🌟 인식한 영창 대본 원문 전체 리턴!
            "target_words_requested": target_words,  # 요청받았던 단어 목록 확인
            "detail_matches": word_check_results,    # 어떤 단어가 맞고 틀렸는지 상세 분석 결과
            "is_matched": is_all_matched             # 🌟 모든 필수 단어가 포함되었는지 최종 통과 여부
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