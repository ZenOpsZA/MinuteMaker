# MinuteMaker

MinuteMaker is a **local meeting transcription pipeline** that converts recorded meetings into clean, speaker-labelled transcripts.

It combines:
- FFmpeg (audio extraction)
- WhisperX (transcription + alignment)
- pyannote (speaker diarization)
- C# post-processing (clean, readable transcript output)

⚠️ This tool runs entirely locally but requires initial model downloads via Hugging Face.

---

## Quick Start

1. Install prerequisites:
   - Python (3.10-3.11 recommended)
   - FFmpeg
   - .NET SDK

2. Set your Hugging Face token:
```powershell
$env:HF_TOKEN="your_token_here"
```

3. Install dependencies:
```powershell
pip install -r Integrations/Python/requirements.txt
```

4. Run:
```powershell
dotnet run
```

---

## Features

- Fully local transcription (no cloud dependency)
- Supports common formats (`.mp4`, `.wav`, `.mp3`, `.m4a`)
- Speaker diarization (Speaker 1, Speaker 2, etc.)
- Interactive speaker name mapping
- Clean, readable transcript output
- Structured output files for further processing
- Designed for governance, meetings, and decision extraction
- 🎧 Optional VLC-assisted speaker identification (jump to speaker timestamps in original recording)

---

## Important Notes

- Speaker diarization is **not 100% accurate**
- Output should be treated as a **draft transcript**
- Manual review is required for:
  - speaker attribution
  - wording corrections

⚠️ First run may take several minutes due to model downloads.

---

## Requirements

### Software

- .NET SDK (tested with .NET 10)
- Recommended: Python 3.10 or 3.11
- Tested with: Python 3.13.9
- FFmpeg (must be available on PATH)
- VLC Media Player (optional, for speaker identification playback)

Verify installation:

```powershell
dotnet --version
python --version
ffmpeg -version
```

---

### Python Dependencies

Install using your preferred environment (recommended: virtual environment):

```powershell
pip install -r Integrations/Python/requirements.txt
```

---

### Hugging Face Setup (Required)

1. Create an account: https://huggingface.co
2. Generate a **read token**
3. Accept access to the diarization model:

   https://huggingface.co/pyannote/speaker-diarization-community-1

⚠️ You must accept access to the pyannote diarization model on Hugging Face, otherwise diarization will fail silently.

Set your token (Windows PowerShell):

```powershell
$env:HF_TOKEN="your_token_here"
```

---

## Known Working Environment

- Python: 3.13.9
- whisperx: 3.8.2
- ffmpeg: 2025 build
- dotnet: 10.0.201

## Project Structure

High-level structure of the application:

```text
MinuteMaker/
  Program.cs
  MinuteMaker.csproj
  README.md
  LICENSE

  Configuration/
    AppConfig.cs
    CleaningOptions.cs

  Models/
    Pipeline/
      PipelinePaths.cs
    Speakers/
      SpeakerSample.cs
    Transcription/
      TranscriptSegment.cs
      WhisperXResult.cs

  Services/
    Audio/
      MediaLauncherService.cs
    Output/
      JsonFileService.cs
      TranscriptFormatter.cs
    Speakers/
      SpeakerMapService.cs
      SpeakerSampleService.cs
    Transcription/
      TranscriptCleaner.cs

  Utilities/
    ProcessRunner.cs

  Integrations/
    Python/
      requirements.txt
      transcribe_diarize.py
```

Namespace layout follows the folder structure, for example:

- `MinuteMaker.Configuration`
- `MinuteMaker.Models.Transcription`
- `MinuteMaker.Services.Speakers`
- `MinuteMaker.Utilities`

---

## Why this project exists

Most transcription tools rely on cloud services and do not provide reliable speaker separation.
MinuteMaker focuses on a local-first workflow with structured outputs that can be used for governance, documentation, and downstream processing.

---

## Setup

### 1. Clone repository

```powershell
git clone https://github.com/ZenOpsZA/MinuteMaker
cd MinuteMaker
```

---

### 2. Install Python dependencies

```powershell
python -m venv .venv
.venv\Scripts\activate
pip install -U pip
pip install -r Integrations/Python/requirements.txt
```

---

### 3. Ensure FFmpeg is available

```powershell
ffmpeg -version
```

---

### 4. Build the application

```powershell
dotnet build
```

---

## Running the Application

```powershell
dotnet run
```

---

### Workflow

1. Enter the folder containing your recordings
2. Select a recording by number
3. The pipeline runs:
   - Extract audio
   - Transcribe
   - Align
   - Diarize
   - Clean transcript
4. Provide speaker names when prompted
5. Review output files

---

### VLC Configuration (Optional)

The tool attempts to open VLC automatically using:

- `vlc` from system PATH
- Common installation locations

If VLC is not detected, you can configure the path manually in `AppConfig`:

```csharp
VlcPath = @"C:\Program Files\VideoLAN\VLC\vlc.exe";
```

---

## Output Structure

For input:

```text
Meeting.mp4
```

You will get:

```text
Meeting.mp4
Meeting_output/
  audio.wav
  output_speakers.json
  transcript_clean.txt
  transcript_review.txt
  speaker-map.json
  python-output.log
```

---

### File Descriptions

| File | Description |
|-----|------------|
| audio.wav | Extracted mono audio |
| output_speakers.json | Raw WhisperX + diarization output |
| transcript_clean.txt | Clean formatted transcript |
| transcript_review.txt | Less-clean review version |
| speaker-map.json | Speaker name mappings |
| python-output.log | Debug log for Python step |

---

## Example Run

```text
Enter folder path containing recordings: D:\Meetings
Available recordings:
1. meeting.mp4

Select recording (1-1): 1

Selected: meeting.mp4
Output folder: D:\Meetings\meeting_output

Step 1/4 - Extracting WAV with FFmpeg.. done

Step 2/4 - Running WhisperX / diarization...
  -> Loading Whisper model done (00:04)
  -> Transcribing / 01:17
  -> Aligning \ 01:29
  -> Running diarization | 03:12

Step 2/4 - WhisperX pipeline complete
```

---

## Performance Expectations

CPU-only processing:

| Task | Time (approx) |
|------|---------------|
| 50 min audio transcription | 30-60 minutes |
| Diarization | 20-40 minutes |

First run may be slower due to model downloads.

---

## Configuration

Configurable in the C# application:

- Python executable path
- Python script path
- Whisper model
- Device (`cpu` / `cuda`)
- Compute type
- Cleaning rules

Defaults:
- CPU mode
- `python` from PATH
- bundled Python pipeline script at `Integrations/Python/transcribe_diarize.py`

---

## Usage

### Speaker Identification

During the speaker naming step, the tool can optionally open the original recording at a representative timestamp for each detected speaker.

This is especially useful for recordings from tools like Microsoft Teams or Zoom, where the active speaker is visually highlighted.

Options during speaker mapping:

- Open recording in VLC at a sample timestamp
- Enter a display name for the speaker
- Keep the default speaker label

This feature helps improve speaker attribution accuracy during manual review.

---

## Troubleshooting

### No JSON output created

- Check `python-output.log`
- Ensure `HF_TOKEN` is set
- Confirm Hugging Face access accepted

---

### FFmpeg not found

- Install FFmpeg
- Add to PATH

---

### Python not found

- Install Python
- Ensure it is on PATH

---

### Diarization fails

- Accept model terms on Hugging Face
- Ensure token is valid

---

### Slow / appears frozen

- First run may download models
- CPU processing is slow but expected

---

## Known Limitations

- Speaker diarization is imperfect
- Overlapping speech reduces accuracy
- CPU-only mode is slow
- Manual transcript validation required

---

## Privacy

- Audio is processed locally
- No cloud transcription required
- Hugging Face is only used for model downloads

---

## Future Improvements (Ideas)

- Speaker identification learning
- Progress estimation based on audio length
- GPU acceleration support
- Automated decision extraction

---

## License

This project is licensed under the MIT License. See `LICENSE` for details.
