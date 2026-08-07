{
  "artifact": "_workspace/current/design/video-review-analysis-amendment11.md",
  "purpose": "Review video analysis and difficulty / group AI specification mapping (AMENDMENT #11)",
  "video": {
    "url": "https://youtu.be/wbDv6nawEeY",
    "title": "아킬레우스: 레전드 언톨드 (Achilles: Legends Untold) 리뷰",
    "uploader": "카사노박TV",
    "uploadDate": "2024-05-03",
    "durationSeconds": 515,
    "transcriptPath": "/tmp/ytana/transcript.txt"
  },
  "tools": {
    "fetch": "yt-dlp",
    "command": "yt-dlp --write-auto-sub --sub-lang ko --skip-download -o /tmp/ytana/transcript https://youtu.be/wbDv6nawEeY"
  },
  "derivedChanges": [
    "Assets/Scripts/Sim/DifficultySpec.cs",
    "Assets/Scripts/Sim/HackTypes.cs",
    "Assets/Scripts/Sim/CinderSim.cs",
    "Assets/Tests/EditMode/DifficultyTests.cs",
    "docs/SIM_SPEC_HACKSLASH.md",
    "_workspace/current/design/video-review-analysis-amendment11.md"
  ]
}
