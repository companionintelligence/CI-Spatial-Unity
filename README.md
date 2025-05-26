# CI-Embodied-Companion-Unity

**CI-Embodied-Companion-Unity** is an immersive avatar-based application designed for Meta Quest 3 and XREAL passthrough devices. It combines real-time camera passthrough with agentic AI analysis via the **Companion Intelligence server**, enabling dynamic, contextual interactions with an embodied virtual companion.

## ✨ Features

- 🧠 **Avatar-powered agentic AI** interactions
- 🪞 **Camera passthrough** integration (Quest 3 & XREAL)
- 🧬 **Real-time environmental analysis** via Companion Intelligence backend
- 🧩 Modular Unity structure, built for experimentation and extension
- 🌐 Cross-device XR compatibility (Quest 3 + XREAL)
- 🎭 Supports custom avatars (e.g., Ready Player Me)

## 🚀 Platforms Supported

- **Meta Quest 3**
- **XREAL Air / Light / Beam (MR Passthrough)**

## 🤖 Agentic Architecture

The system performs **context-aware analysis** of passthrough visuals by streaming metadata and captured context to a **Companion Intelligence server**, which performs reasoning and returns behavioral or dialog updates for the avatar. This enables:

- Spatially aware AI responses
- Ambient presence and continuous learning
- Real-world object recognition and interaction planning

## 🛠️ Tech Stack

- **Unity 6.0** (Built-In RP)
- **XR Interaction Toolkit**
- **Meta XR SDK**
- **XREAL NRSDK**
- **Custom Companion Intelligence API**
- **Ready Player Me** (optional)
- **OpenAI or LLM-compatible backends**

## ⚙️ Unity Quest API Installation

This project uses a custom Unity Quest API package.

> ✅ This works with any **modern Unity version**, tested with **Unity 6.0+**.

> ⚠️ If using **Unity versions < 6.0**, you **must include**:
> ```
> com.utilities.async
> ```

### Native Android Plugin Support

Since this package includes a **native AAR plugin**, it depends on:

**External Dependency Manager for Unity (EDM4U)**

You may already have EDM4U if you use **Google Play**, **Firebase**, or similar SDKs. If not:

➡️ [EDM4U – Getting Started](https://github.com/googlesamples/unity-jar-resolver)

## 📁 Project Structure

CI-Embodied-Companion-Unity/ ├── Assets/ ├── Packages/ ├── ProjectSettings/ ├── README.md └── ...

Set Unity Ver 2022.3 LTS


## COPYRIGHT COMPANION INTELLIGENCE

## DO NOT DISTRIBUTE

## PRIVATE AND CONFIDENTIAL


### todo
#### Ready Player Me 
- head tends to move every time a new sentence
- eyes pop a bit; also body pose pops on start


Based on
https://github.com/companionintelligence/embodied-agent-client
