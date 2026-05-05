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

MinuteMaker loads optional local settings from `appsettings.json`. For local development, put this file here:

```text
MinuteMaker/appsettings.json
```

The app also copies `appsettings.json` to the build output when present. At startup, MinuteMaker prints the exact config path it found:

```text
Config file checked : D:\Repo\MinuteMaker\MinuteMaker\MinuteMaker\appsettings.json
Config file exists  : True
```

If no file exists, CPU mode remains the default.

CPU example:

```json
{
  "WhisperX": {
    "ExecutablePath": "whisperx",
    "Model": "base",
    "Device": "cpu",
    "ComputeType": "int8",
    "BatchSize": 4
  },
  "TranscriptionLanguage": "en"
}
```

GPU example:

```json
{
  "WhisperX": {
    "ExecutablePath": ".venv-whisperx-gpu\\Scripts\\whisperx.exe",
    "Model": "base",
    "Device": "cuda",
    "ComputeType": "float16",
    "BatchSize": 8
  },
  "TranscriptionLanguage": "en"
}
```

In CUDA mode, MinuteMaker validates that the configured `whisperx.exe` exists and that the adjacent `python.exe` reports `True` for `torch.cuda.is_available()` before transcription starts.

Avoid using `"ExecutablePath": "whisperx"` with `"Device": "cuda"`. That can resolve to a global Python or CPU-only install. Use the explicit venv executable path instead.

---

## CUDA / GPU WhisperX Setup

Use a separate virtual environment for GPU transcription so the normal CPU setup stays clean.

### 1. Check the NVIDIA driver

```powershell
nvidia-smi
```

This should show your NVIDIA GPU and driver version. If this command is missing or cannot see the GPU, fix the NVIDIA driver before continuing.

### 2. Create the GPU WhisperX environment

Run these commands from the repo root:

```powershell
py -3.11 -m venv .venv-whisperx-gpu
.\.venv-whisperx-gpu\Scripts\activate
python -m pip install --upgrade pip setuptools wheel
```

Python 3.10 or 3.11 is recommended for WhisperX.

### 3. Install PyTorch with CUDA

Use the official PyTorch install selector for the current recommended command:

```text
https://pytorch.org/get-started/locally/
```

For example, on Windows with a CUDA 12.8-compatible driver, the command currently looks like:

```powershell
pip install torch torchvision torchaudio --index-url https://download.pytorch.org/whl/cu128
```

If the PyTorch selector recommends a different CUDA wheel for your machine, use that command instead.

Verify CUDA from inside the venv:

```powershell
python -c "import torch; print(torch.__version__); print(torch.version.cuda); print(torch.cuda.is_available())"
```

The last line must be:

```text
True
```

### 4. Install WhisperX

With the GPU venv still activated:

```powershell
pip install whisperx==3.8.2
```

Check that the executable exists:

```powershell
Test-Path .\.venv-whisperx-gpu\Scripts\whisperx.exe
```

This should also return:

```text
True
```

If installing WhisperX changes the PyTorch package, rerun the PyTorch CUDA install command from step 3 and check `torch.cuda.is_available()` again.

### 5. Create the local GPU config

Copy the GPU example config to the local app config file:

```powershell
Copy-Item .\MinuteMaker\appsettings.gpu.example.json .\MinuteMaker\appsettings.json
```

The important section is:

```json
{
  "WhisperX": {
    "ExecutablePath": ".venv-whisperx-gpu\\Scripts\\whisperx.exe",
    "Model": "base",
    "Device": "cuda",
    "ComputeType": "float16",
    "BatchSize": 8
  },
  "TranscriptionLanguage": "en"
}
```

MinuteMaker resolves relative `ExecutablePath` values from the solution/repo root and config file directory before falling back to the build output folder. This means:

```json
".venv-whisperx-gpu\\Scripts\\whisperx.exe"
```

resolves to:

```text
D:\Repo\MinuteMaker\MinuteMaker\.venv-whisperx-gpu\Scripts\whisperx.exe
```

### 6. Run and monitor GPU use

Set your Hugging Face token in the same terminal session:

```powershell
$env:HF_TOKEN="your_token_here"
dotnet run --project .\MinuteMaker\MinuteMaker.csproj
```

In another terminal, watch the GPU:

```powershell
nvidia-smi -l 1
```

During WhisperX transcription you should see a Python or WhisperX process using GPU memory. MinuteMaker also prints the executable, model, device, compute type, and batch size before starting WhisperX.

In GPU mode, the startup output should include details like:

```text
WhisperX configuration:
  config path      : D:\Repo\MinuteMaker\MinuteMaker\MinuteMaker\appsettings.json
  config exists    : True
  executable input : .venv-whisperx-gpu\Scripts\whisperx.exe
  executable final : D:\Repo\MinuteMaker\MinuteMaker\.venv-whisperx-gpu\Scripts\whisperx.exe
  model            : large-v3
  device           : cuda
  compute type     : float16
  batch size       : 8
  command          : "D:\Repo\MinuteMaker\MinuteMaker\.venv-whisperx-gpu\Scripts\whisperx.exe" ...
CUDA validation:
  python.exe : D:\Repo\MinuteMaker\MinuteMaker\.venv-whisperx-gpu\Scripts\python.exe
  stdout     : D:\Repo\MinuteMaker\MinuteMaker\.venv-whisperx-gpu\Scripts\python.exe
  stdout     : 2.8.0+cu128
  stdout     : True
  stdout     : NVIDIA GeForce RTX 5060
```

The same launch summary is written at the top of `python-output.log`, which makes it easy to confirm later that the run used the GPU venv rather than a global Python install.

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
