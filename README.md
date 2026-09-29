# iSent.Magician

**iSent.Magician** is a high-performance industrial vision inspection and defect classification system built on **.NET 9 (x64)** and **MVTec HALCON**. It combines classical frequency-domain / spatial signal processing with deep learning classification to perform real-time surface anomaly detection and automated defect annotation on industrial camera frames.

---

## 🌟 Key Features

- **Hybrid Inspection Pipeline**:
  - **Classical Pre-processing**: Surface anomaly detection using Difference of Gaussians (DoG) and FFT Sine Bandpass filters.
  - **Tile-based Region Extraction & Mosaicking**: Dynamic anomaly region grouping and grid slicing into fixed-size tiles (e.g., 256x256).
  - **Deep Learning Classification**: Batch inference via HALCON DL models (`.hdl`) with GPU acceleration support.
  - **Real-Time Bounding Box Mapping & Filtering**: Computes precise bounding boxes and filters out non-critical or low-confidence classes.
- **Flexible Image Acquisition**:
  - `folder`: Round-robin batch processing of images (`.jpg`, `.png`, `.bmp`, `.tiff`, `.tif`, `.raw`) from disk.
  - `gige`: Native acquisition from industrial **GigE Vision 2** cameras via HALCON Framegrabber.
  - `ros`: Subscribed camera stream from **ROS Bridge** over MQTT (JSON Base64 or raw bytes).
- **Embedded & Distributed Communications**:
  - **Embedded MQTT Broker** (`MQTTnet.Server`) running natively on port `1883`.
  - **MQTT Client Service** supporting bidirectional robot telemetry synchronization (pose, speed, sensing active states) and publishing structured inspection results.
- **Automated Defect Overlay & Dataset Saving**:
  - Asynchronous color-coded overlay generation and saving of annotated defect frames organized by date.

---

## 📁 Solution Architecture

```
iSent.Magician/
├── iSent.Magician.sln                     # Visual Studio / JetBrains Rider Solution
├── README.md                              # Project documentation
│
└── iSent.Magician.ConsoleApp/             # Application Host & Runtime Orchestrator
    ├── Components/
    │   └── processing_anomalies.hdev      # HDevelop procedure definitions
    ├── Libs/
    │   └── iSent.Magician.Engine.dll      # Core Inspection & Vision Library
    ├── Models/                            # Default model assets (.hdl, .hdict, .tiff)
    ├── Program.cs                         # Application entry point, worker loops & shutdown handler
    ├── Resources/                         # Sample test images
    └── Services/
        ├── MqttBrokerService.cs           # Embedded MQTT broker hosting
        ├── MqttClientService.cs           # MQTT client, topic subscriptions & result publishing
        ├── mqtt_config.yaml               # Broker and topic configuration file
        └── ImageSources/
            ├── IImageSourceService.cs     # Abstraction for image capture providers
            ├── FileFolderImageSource.cs   # File directory image feeder
            ├── HalconGigEVisionImageSource.cs # Industrial GigE Vision 2 capture driver
            └── RosBridgeMqttImageSource.cs# ROS Bridge MQTT image feeder
```

---

## 🛠️ Prerequisites & Requirements

- **Operating System**: Windows 10 / Windows 11 / Windows Server (64-bit)
- **Runtime / SDK**: [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) (x64)
- **MVTec HALCON**:
  - HALCON 25.11+ installed with valid license.
  - Required native libraries (`halcon.dll`, `halcondotnet.dll`, `hdevengine.dll`, etc.) must be accessible in your system `PATH` or HALCON environment variables (`%HALCONROOT%\bin\x64-win64`).
- **Hardware Acceleration (Optional)**:
  - NVIDIA GPU with CUDA & cuDNN support compatible with your HALCON version for accelerated deep learning inference.

---

## ⚙️ Configuration

### 1. MQTT Configuration (`Services/mqtt_config.yaml`)

```yaml
host: "127.0.0.1"
port: 1883
robot_topics_mqtt:
  - "robot/position"
  - "robot/speed"
  - "robot/cobot_mode"
  - "robot/sensing/finished"
  - "robot/sensing/active"
inspection_mqtt_topic: "inspection/result"
```

### 2. Image Processing & Model Configuration (`ImageProcessingConfig`)

Configured programmatically or adjusted in `ImageProcessingConfig.cs` / `Program.cs`:

| Property | Default Value | Description |
| :--- | :--- | :--- |
| `PathToPreprocessParamFile` | `Models/model_optimized_dl_preprocess_params.hdict` | HALCON preprocessing dictionary |
| `PathToTrainedModelFile` | `Models/model_optimized.hdl` | Trained deep learning model file |
| `ImageWidth` / `ImageHeight` | `3000` / `4096` | Expected input image dimensions |
| `TileWidth` / `TileHeight` | `256` / `256` | Crop tile size for candidate defect regions |
| `MinScoreClass` | `0.40` | Minimum confidence score threshold |
| `BatchSizeInference` | `50` | Number of tiles processed per inference batch |
| `IgnoredClasses` | `["clean", "suspicious", "Dust", "Background"]` | Classes excluded from final defect detections |

---

## 🚀 Getting Started & Execution

### Build Solution

```powershell
dotnet build iSent.Magician.sln -c Release -p:Platform=x64
```

### Run Options

#### 1. Folder Mode (Default)
Reads images continuously from a local directory in a round-robin loop:

```powershell
# Default Resources folder
dotnet run --project iSent.Magician.ConsoleApp -c Release

# Custom directory
dotnet run --project iSent.Magician.ConsoleApp -c Release -- --source folder --path "C:\Path\To\Images"
```

#### 2. GigE Vision Camera Mode
Captures live frames directly from an industrial GigE Vision camera:

```powershell
dotnet run --project iSent.Magician.ConsoleApp -c Release -- --source gige
```

#### 3. ROS Bridge Mode
Listens for image frames streamed over MQTT / ROS Bridge:

```powershell
dotnet run --project iSent.Magician.ConsoleApp -c Release -- --source ros
```

### Graceful Termination
Press `Q` in the console window or press `Ctrl + C` to trigger an orderly shutdown of all capture sources, MQTT services, and unmanaged HALCON handles.

---

## 📊 Inspection Result Format

Inspection results are serialized into JSON and published to the configured topic (`inspection/result`):

```json
{
  "timestamp_sent": 1727624400,
  "inspection_pose": {
    "position": { "x": 120.5, "y": 450.2, "z": 890.0 },
    "orientation": { "x": 0.0, "y": 0.0, "z": 0.7071, "w": 0.7071 }
  },
  "detections": [
    {
      "tile_idx": 14,
      "bbox": {
        "x": 1024,
        "y": 768,
        "width": 256,
        "height": 256
      },
      "results": [
        {
          "label": "PositiveDent",
          "score": 0.94
        }
      ]
    }
  ]
}
```

---

## 🏷️ Defect Classes & Visualization

Detected defects are highlighted on saved annotated images under `%USERPROFILE%\Documents\Magician_Dataset\Annotated\yyyy-MM-dd\` with distinct color codes:

| Defect Class | Bounding Box Color |
| :--- | :--- |
| `PositiveDent` | Red (`#FF0000`) |
| `NegativeDent` | Magenta (`#FF00FF`) |
| `Scratch` | Cyan (`#00FFFF`) |
| `Dust` | Orange (`#FFA500`) |
| `SealResiduals` | Beige (`#F5F5DC`) |
| `Suspicious` | Yellow (`#FFFF00`) |
| `Clean` | Green (`#00FF00`) |
| *Other / Default* | Cyan (`#00FFFF`) |
