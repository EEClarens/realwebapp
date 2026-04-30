# 📐 Irregular Solid Volume Calculator

A multi-language project for computing volumes of irregular solids using the **Disk/Washer Method** and **Composite Simpson's Rule**.

| Layer | Language | Description |
|-------|----------|-------------|
| 🌐 Frontend | **JavaScript** | Interactive 3D WebGL-style renderer, runs in browser |
| ⚙️ Backend API | **C# / ASP.NET Core** | REST API (`POST /api/volume`) — the recommended engine |
| 🖥️ Desktop App | **Visual Basic .NET** | Standalone WinForms app with live 3D mesh preview |

---

## 🚀 Quick Start

### 1 · Run the C# API (recommended)

Requires [.NET 8 SDK](https://dotnet.microsoft.com/download)

```bash
cd api
dotnet run
# → Listening on http://localhost:5050
```

### 2 · Open the Web App

```bash
# Just open in browser — no build step needed
open webapp/index.html
# or: python -m http.server 8080 (then visit localhost:8080/webapp)
```

The webapp auto-pings `http://localhost:5050` and will use the **C# engine** if the API is running, otherwise falls back gracefully to the **JavaScript engine**.

### 3 · Run the VB.NET Desktop App (Windows)

```bash
cd vbapp
dotnet run
# OR compile manually:
vbc IrregularVolumeCalculator.vb /target:winexe /r:System.Windows.Forms.dll /r:System.Drawing.dll
```

---

## 📡 C# API Reference

### `POST /api/volume`

**Request body:**
```json
{
  "solidIndex": 0,
  "l": 10.0,
  "r": 5.0,
  "a": 1.0,
  "n": 3.0
}
```

**Response:**
```json
{
  "solidName": "Sinusoidal Paraboloid Shell",
  "volume": 942.477796,
  "unit": "cm³",
  "method": "Disk/Washer + Composite Simpson's Rule",
  "slices": 1000,
  "deltaX": 0.01,
  "language": "C# (ASP.NET Core Minimal API)"
}
```

### `GET /api/solids` — List all 5 solid definitions

### `GET /api/health` — Health check

---

## 🔷 Solid Forms

| # | Name | Formula |
|---|------|---------|
| 0 | Sinusoidal Paraboloid Shell | `r(x) = R·(1−(x/L)²) + A·sin(N·π·x/L)` |
| 1 | Damped-Wave Frustum | `r(x) = R + (A−R)·(x/L) + N·cos(3x)·e^(−0.3x)` |
| 2 | Exponential Ogive Dome | `r(x) = R·√(1−(x/L)²)·e^(−A·x/L)` |
| 3 | Hyperbolic Annular Solid | `r(x) = √(R² + (x−L/2)²/A²)` |
| 4 | Polynomial Biconcave Spindle | `r(x) = R·[4·(x/L)·(1−x/L)]^N` |

---

## 📁 Project Structure

```
IrregularVolumeCalculator/
├── api/                          ← C# ASP.NET Core REST API
│   ├── VolumeApi.cs              ← Minimal API entrypoint + Simpson logic
│   ├── VolumeApi.csproj          ← .NET 8 project file
│   └── appsettings.json          ← Port 5050 config
│
├── webapp/                       ← Web frontend (no build step)
│   └── index.html                ← Full app: 3D viewer + C#/JS engine toggle
│
├── vbapp/                        ← VB.NET WinForms desktop app
│   ├── IrregularVolumeCalculator.vb
│   └── IrregularVolumeCalculator.vbproj
│
└── README.md
```

---

## 🧮 Math: How it works

Volume via the **Disk/Washer Method**:

```
V = π ∫₀ᴸ [r(x)]² dx
```

Approximated with **Composite Simpson's Rule** (n = 1000 slices):

```
V ≈ (h/3) · [f(x₀) + 4f(x₁) + 2f(x₂) + 4f(x₃) + ... + f(xₙ)]
where h = L/n
```

---

## 📋 Requirements

| Component | Requirement |
|-----------|-------------|
| C# API | .NET 8 SDK |
| Web App | Any modern browser |
| VB Desktop | .NET 6+ SDK (Windows) |

---

## 📄 License

MIT — free to use, modify, and distribute.
