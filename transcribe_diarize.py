import argparse
import json
import os
import whisperx


def log_stage(message):
    print(f"STAGE: {message}", flush=True)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", required=True, help="Path to input wav")
    parser.add_argument("--output", required=True, help="Path to output json")
    parser.add_argument("--model", default="base", help="Whisper model name")
    parser.add_argument("--device", default="cpu", help="cpu or cuda")
    parser.add_argument("--compute-type", default="int8", help="Compute type")
    parser.add_argument("--batch-size", type=int, default=4, help="Batch size")
    args = parser.parse_args()

    hf_token = os.environ.get("HF_TOKEN")
    if not hf_token:
        raise RuntimeError("HF_TOKEN environment variable was not supplied.")

    audio_file = args.input
    device = args.device
    batch_size = args.batch_size

    log_stage("Loading Whisper model")
    model = whisperx.load_model(args.model, device, compute_type=args.compute_type)

    log_stage("Transcribing")
    result = model.transcribe(audio_file, batch_size=batch_size)

    log_stage("Loading alignment model")
    model_a, metadata = whisperx.load_align_model(
        language_code=result["language"],
        device=device
    )

    log_stage("Aligning")
    result = whisperx.align(
        result["segments"],
        model_a,
        metadata,
        audio_file,
        device
    )

    log_stage("Running diarization")
    from whisperx.diarize import DiarizationPipeline

    diarize_model = DiarizationPipeline(
        device=device,
        token=hf_token
    )

    diarize_segments = diarize_model(audio_file)

    log_stage("Assigning speakers")
    result = whisperx.assign_word_speakers(diarize_segments, result)

    log_stage("Saving output")
    with open(args.output, "w", encoding="utf-8") as f:
        json.dump(result, f, ensure_ascii=False, indent=2)

    log_stage("Complete")


if __name__ == "__main__":
    main()