from fastapi import FastAPI, UploadFile, File, Form
from transformers import pipeline
import librosa
import numpy as np
import soundfile as sf
import io
import os

# 🌟 [치트키] 백그라운드(PM2) 구동 시 우분투 시스템의 ffmpeg를 못 찾는 문제를 완벽 차단
os.environ["PATH"] += os.pathsep + "/usr/bin"
os.environ["PATH"] += os.pathsep + "/usr/local/bin"

app = FastAPI()

# 1. Wav2Vec2 감정 분석 파이프라인 로드
emotion_classifier = pipeline("audio-classification", model="Dpngtm/wav2vec2-emotion-recognition")

# 2. Whisper STT(음성 인식) 파이프라인 추가 (⭐ 한국어 인식을 위한 명시적 옵션 주입)
stt_pipeline = pipeline(
    "automatic-speech-recognition", 
    model="openai/whisper-tiny",
    generate_kwargs={"language": "ko", "task": "transcribe"}
)

@app.post("/analyze-audio")
async def analyze_audio(
    file: UploadFile = File(...), 
    default_pitch: float = Form(150.0),
    target_words: str = Form("") 
):
    # 파일 읽기
    audio_bytes = await file.read()
    data, samplerate = sf.read(io.BytesIO(audio_bytes))
    
    # 16kHz 리샘플링 (Wav2Vec2 및 Whisper 최적화 사양)
    if samplerate != 16000:
        data = librosa.resample(data, orig_sr=samplerate, target_sr=16000)
        samplerate = 16000

    # 동시 요청 충돌을 예방하기 위해 프로세스 ID(PID) 기반 임시 파일명 생성
    temp_filename = f"temp_{os.getpid()}.wav"
    sf.write(temp_filename, data, samplerate)
    
    try:
        # --- 1. 음성 인식 (STT) 수행 및 영창 대본 추출 ---
        stt_result = stt_pipeline(temp_filename)
        recognized_text = stt_result.get("text", "").strip() 
        
        # ⭐ [디버깅 추가] PM2 logs 혹은 터미널 창에서 들어온 소리와 텍스트를 라이브로 대조 확인
        print("\n" + "="*50)
        print(f"🎙️ [AI 서버 디버깅] 실제 인식된 대사(STT): '{recognized_text}'")
        print(f"🎯 [AI 서버 디버깅] 유니티가 요구한 정답 단어 목록: '{target_words}'")
        print("="*50 + "\n")
        
        # --- 2. 다중 필수 단어 검증 로직 ---
        is_all_matched = False
        word_check_results = {} 
        
        if target_words:
            # 공백 제거 및 소문자화된 인식 대본 준비
            clean_recognized = recognized_text.replace(" ", "").lower()
            
            # 쉼표로 분리하여 검증할 단어 배열 생성
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
            is_all_matched = True

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
                "recognized_text": recognized_text,      
                "target_words_requested": target_words,  
                "detail_matches": word_check_results,    
                "is_matched": is_all_matched             
            },
            "emotions": {res['label']: round(res['score'] * 100, 1) for res in emotion_results},
            "audio_features": {
                "current_pitch": round(current_pitch, 1),
                "pitch_ratio": round(pitch_ratio, 2),
                "duration_seconds": round(duration, 2)
            }
        }
    finally:
        # 💡 예외 발생 여부와 상관없이 파일 청소 보장
        if os.path.exists(temp_filename):
            os.remove(temp_filename)

if __name__ == "__main__":
    import uvicorn
    uvicorn.run("emotion_server:app", host="127.0.0.1", port=5000, reload=True)