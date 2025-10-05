# Laberinto
Versión de Unity: <anota aquí la versión exacta del Hub>
## Abrir
1) git clone <URL>
2) Unity Hub → Open → carpeta clonada
3) Abrir `Assets/Scenes/Main.unity`
## Ramas
- main (estable)
- dev (integración)
- feature/<area>-<nombre>

## Instalación (Python)

Requisitos: Python 3.10–3.12

1) Crear ambiente virtual
```bash
cd python
python -m venv .venv
# Windows:
.\.venv\Scripts\activate
# macOS/Linux:
source .venv/bin/activate
```

```mermaid
classDiagram
direction LR

class ISensorProvider {
  <<interface>>
  +float get Concentration01
  +bool ConsumeBlinkRight()
  +bool ConsumeBlinkLeft()
}

class SensorMock {
  +float Concentration01
  +bool ConsumeBlinkRight()
  +bool ConsumeBlinkLeft()
  -float _concentration
  -Queue<bool> _rightBlinks
  -Queue<bool> _leftBlinks
}

class UnicornCsvReader {
  +float Concentration01
  +bool ConsumeBlinkRight()
  +bool ConsumeBlinkLeft()
  -string _csvPath
  -float _baseline
  -float _lastConc
  -RingBuffer<float> _concWindow
  -BlinkDetector _blink
  -FileWatcher _watcher
  -void ParseRow(string row)
  -float MapEEGToConc(float[] bands)
}

class BlinkDetector {
  +bool TryConsumeRight()
  +bool TryConsumeLeft()
  -float _cooldownSec
  -float _lastRightTime
  -float _lastLeftTime
  -bool Detect(IMUSample imu)
}

class PlayerMotor {
  +float baseSpeed
  +float turnAngle
  +float turnCooldown
  +void InjectSensor(ISensorProvider p)
  -ISensorProvider _sensor
  -Rigidbody2D _rb
  -Vector2 _forward
  -float _lastTurnTime
  -void Update()
  -void FixedUpdate()
}

class GameManager {
  +static GameManager I
  +GameState State
  +event Action OnLevelComplete
  +void SetSensorProvider(ProviderType t)
  +void LoadNextLevel()
  -ISensorProvider _provider
  -LevelLoader _loader
}

class LevelLoader {
  +void LoadNext()
  +void Reload()
  -int _currentIndex
}

class SensorDebugUI {
  +void Bind(ISensorProvider p)
  -ISensorProvider _sensor
  -Slider _concBar
  -Text _blinkInfo
  -void Update()
}

class CheckpointQuizTrigger {
  +string QuizId
  -void OnTriggerEnter2D(Collider2D other)
}

class IMUSample {
  +float gx
  +float gy
  +float gz
  +float ax
  +float ay
  +float az
}

class ProviderType {
  <<enum>>
  Mock
  UnicornCsv
}

class GameState {
  <<enum>>
  Boot
  Playing
  Paused
  LevelComplete
}

ISensorProvider <|.. SensorMock
ISensorProvider <|.. UnicornCsvReader
PlayerMotor --> ISensorProvider : usa
GameManager --> ISensorProvider : posee
GameManager --> LevelLoader
SensorDebugUI --> ISensorProvider : lee
UnicornCsvReader --> BlinkDetector
UnicornCsvReader --> IMUSample
GameManager --> GameState
GameManager --> ProviderType
CheckpointQuizTrigger --> GameManager : notifica

```
