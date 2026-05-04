# Практическая работа №4; Батов Даниил ЭФБО-10-23

Учебная веб-служба на ASP.NET Core, моделирующая **четырёхшаговое бронирование переговорки** с идемпотентностью по событию, компенсацией при сбое шага, журналированием со сквозным **Correlation ID** и метриками в формате Prometheus.

---

## 1. Запуск

**Запуск из корня репозитория:**

```bash
dotnet run --project BookingService
```

URL по умолчанию задаётся в `BookingService/Properties/launchSettings.json` (часто `http://localhost:5139`). Другой адрес:

```bash
dotnet run --project BookingService --urls http://localhost:8080
```

**Сборка:**

```bash
dotnet build BookingService
```

**Готовые HTTP-примеры** для IDE (REST Client / VS Code): файл `BookingService/BookingService.http`.

---

---

## 2. Назначение ключей (эксплуатация)

| Понятие | Где задаётся | Назначение |
|---------|--------------|------------|
| **Ключ процесса** | Путь URL: `/api/processes/{processKey}/...` | Однозначно идентифицирует экземпляр саги бронирования. Состояние хранится в памяти по этому ключу. |
| **Ключ идемпотентности** | JSON-тело: `idempotencyKey` | Однозначно идентифицирует **доставку события** в рамках процесса. Повтор с тем же ключом не меняет состояние и учитывается как redelivery. |
| **Correlation ID** | Заголовок `X-Correlation-Id` (необязательно) | Связывает записи в журнале и поле `correlationId` в ответе; при отсутствии заголовка генерируется и возвращается в ответе. |

Данные процессов **не переживают перезапуск** приложения (in-memory store).

---

## 3. Краткая карта каталогов

```
PR4 Batov.sln
BookingService/
  Program.cs                 — хост, маршруты, DI
  appsettings*.json          — конфигурация
  BookingService.http        — примеры запросов
  Api/                       — DTO и хелперы HTTP
  Domain/                    — состояния и события
  Services/                  — хранилище, сага, здоровье по ошибкам
  Telemetry/                 — метрики Meter
  Middleware/                — correlation id
  Health/                    — readiness check
  Options/                   — сильно типизированные опции
  Properties/launchSettings.json
```
