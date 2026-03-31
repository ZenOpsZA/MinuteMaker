# MinuteMaker

> Local-first meeting transcription and speaker correction system

![.NET](https://img.shields.io/badge/.NET-10-blue)
![Python](https://img.shields.io/badge/Python-3.10%2B-yellow)
![License](https://img.shields.io/badge/License-MIT-green)
![Status](https://img.shields.io/badge/Status-Active-success)

---

## Overview

MinuteMaker converts recorded meetings into **clean, speaker-labelled transcripts** using a fully local pipeline.

Unlike typical transcription tools, it treats speaker diarization as **draft output** and provides a structured **human-in-the-loop correction workflow** to produce reliable results.

---

## Key Capabilities

* Local transcription (no cloud dependency)
* WhisperX transcription + alignment
* pyannote speaker diarization
* Guided speaker correction workflow
* Bucket-first speaker assignment
* Suspicious segment detection
* Scoped corrections (segment, run, bucket)
* Resumable correction sessions
* Clean and review transcript outputs
* VLC-assisted speaker identification (optional)

---

## Why MinuteMaker

Most tools:

* rely on cloud APIs
* provide weak speaker separation
* require manual cleanup from scratch

MinuteMaker:

* runs locally
* structures the correction process
* reduces manual effort significantly
* produces outputs suitable for governance and operational use

---

## Quick Start

### Prerequisites

* .NET SDK (tested with .NET 10)
* Python 3.10–3.11 recommended
* FFmpeg (on PATH)
* VLC (optional)

### Setup

```powershell
git clone https://github.com/ZenOpsZA/MinuteMaker
cd MinuteMaker

python -m venv .venv
.venv\Scripts\activate
pip install -r Integrations/Python/requirements.txt
```

Set Hugging Face token:

```powershell
$env:HF_TOKEN="your_token_here"
```

### Run

```powershell
dotnet run
```

---

## Workflow

```text
Recording
   ↓
Audio Extraction (FFmpeg)
   ↓
Transcription + Alignment (WhisperX)
   ↓
Speaker Diarization (pyannote)
   ↓
Correction Workspace (C#)
   ↓
Guided Correction Workflow
   ↓
Final Transcript Output
```

---

## Correction System

MinuteMaker introduces a structured correction model:

* **Speaker Buckets** → group segments by diarized speaker
* **Review Runs** → contiguous speaker segments
* **Suspicious Items** → flagged for review

### Correction Scopes

* Segment
* Review run
* Speaker bucket

### Override Precedence

1. Segment override
2. Run override
3. Bucket override
4. Raw diarization

---

## Output Structure

```text
Meeting_output/
  audio.wav
  output_speakers.json
  speaker-corrections.json
  transcript_clean.txt
  transcript_review.txt
  speaker-map.json
  python-output.log
```

### Key Principle

* Raw output is **never modified**
* Corrections are stored separately
* Final transcript is **projected from overrides**

---

## Performance

CPU-only baseline:

| Task                 | Time      |
| -------------------- | --------- |
| 50 min transcription | 30–60 min |
| Diarization          | 20–40 min |

---

## Configuration

Configurable via C#:

* Python path
* Whisper model
* Device (`cpu` / `cuda`)
* Cleaning rules

---

## VLC Integration (Optional)

* Jump to timestamps in original recording
* Useful for identifying speakers in video recordings

---

## Limitations

* Diarization is imperfect
* Overlapping speech reduces accuracy
* CPU processing is slow
* Manual validation still required

---

## Privacy

* Fully local processing
* No cloud transcription
* Hugging Face used only for model download

---

## Roadmap

* Structured extraction (decisions, actions, summaries)
* Persistent speaker identity
* Improved correction heuristics
* Optional UI layer
* Performance optimisation

---

## Project Structure

```text
Models/
  Corrections/
  Pipeline/
  Speakers/
  Transcription/

Services/
  Corrections/
  Audio/
  Output/
  Speakers/
  Transcription/

Persistence/
  Corrections/

Integrations/
  Python/
```

---

## Contributing

See `CONTRIBUTING.md` for guidelines.

---

## License

MIT License
