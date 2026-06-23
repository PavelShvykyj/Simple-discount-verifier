# Backend API Implementation Plan

Дата: 2026-06-20

Этот документ описывает план реализации backend-составляющей MVP. Он опирается
на текущие документы:

- `docs/architecture/api-contract.md`
- `docs/architecture/table-storage-design.md`
- `docs/architecture/data-lifecycle.md`
- `docs/architecture/architecture-decisions.md`
- `docs/architecture/observability.md`
- `docs/architecture/backend-api-implementation-readiness.md`
- `docs/architecture/backend-api-git-pr-strategy.md`
- `docs/product/discount-verification-summary.md`

## 1. Общая Картина

Backend остается внутри Azure Static Web Apps managed Functions:

```text
Angular/Ionic SPA
  -> same-origin /api
    -> .NET isolated Azure Functions
      -> application use cases
        -> domain rules
        -> infrastructure adapters
          -> Azure Table Storage
          -> SMS-Fly
          -> Application Insights
```

Публичные, административные и POS API имеют разные границы доверия:

- `/api/public/*` доступен клиенту без логина и управляет redemption flow.
- `/api/backoffice/*` защищен Static Web Apps ролью `admin`.
- `/api/pos/*` открыт на SWA edge как `anonymous`, но внутри Function обязан
  пройти HMAC-проверку.
- `/api/scanner-survey` уже реализован и остается отдельным временным admin
  инструментом.

Сервис не считает скидку. Его ответственность: анкеты, SMS-подтверждение,
одноразовый barcode, POS-валидация, on-demand profile lookup и audit trail.

## 2. Архитектурные Правила

Реализацию держим простой, но с чистыми границами:

- Function entrypoints только читают HTTP, вызывают use case и мапят ответ.
- Use case содержит сценарий приложения, но не знает деталей Azure Table SDK.
- Domain-компоненты отвечают за правила: телефон, анкета, barcode, TTL, HMAC,
  hashing, статусы и ошибки.
- Infrastructure содержит Azure Table adapters, SMS-Fly client, telemetry и
  конфигурацию.
- DTO API-контракта не смешиваются с storage entity и domain-моделями.
- Секреты и connection strings читаются только из server-side configuration.
- `AuditEvents` хранит бизнес/audit события, Application Insights только
  операционную телеметрию.
- Sensitive data не пишем в Application Insights: raw phone, SMS code,
  barcode value, request body.

Для KISS на MVP backend остается в одном `functions` project, но код
разделяется по папкам. Отдельные сборки не вводим в текущем плане; их можно
рассмотреть позже, если один проект начнет мешать навигации или тестам.

Предлагаемая структура:

```text
functions/
  Functions/
    Public/
    Admin/
    Pos/
    System/
  Application/
    Redemptions/
    CustomerProfiles/
    Pos/
    Audit/
  Domain/
    CustomerProfiles/
    Redemptions/
    Security/
    Shared/
  Infrastructure/
    Options/
    Storage/
    Sms/
    Telemetry/
  Contracts/
    Public/
    Admin/
    Pos/
    Common/
```

## 3. Главные Потоки

### 3.1 Admin Profile Management

Администратор создает и обновляет анкеты. Таблица `CustomerProfiles` является
источником сохраненных eligibility-профилей. Ключ профиля - нормализованный
телефон без `+`.

Сначала реализуем этот поток, потому что публичный redemption зависит от
существования анкеты.

### 3.2 Public Redemption

Клиент вводит телефон. Backend сразу создает `correlationId`, пишет
`redemption_started`, нормализует телефон, ищет анкету, отправляет SMS и
создает/перезаписывает текущую строку `DiscountRuntime`.

Если анкета не найдена или телефон невалиден, `AuditEvents` все равно должен
содержать событие с `correlationId`, если backend успел его создать.

### 3.3 SMS Verification And Barcode Issue

Backend читает `DiscountRuntime` по `redemptionKey`, проверяет TTL и попытки,
сравнивает hash SMS-кода и при успехе выпускает barcode:

```text
<phoneRuntimeKey10><correlationId10>
```

Barcode всегда состоит из 20 uppercase base32 символов: первые 10 символов -
полный `phoneRuntimeKey`, последние 10 символов - полный `correlationId`.
Префиксов и разделителей нет; POS отличает web-code по длине. В storage
сохраняется только `BarcodeHash`.

### 3.4 POS Barcode Validation

POS вызывает `/api/pos/barcodes/validate` с HMAC headers и `scanId`. Backend
парсит barcode по фиксированным offsets, читает текущий runtime row по
`phoneRuntimeKey`, проверяет `correlationId`, TTL/hash и потребляет barcode
через ETag-aware update.

Повтор с тем же `scanId` возвращает idempotent success. Повтор с другим
`scanId` возвращает `already_used`.

### 3.5 POS Profile Lookup

POS может запросить анкету по телефону после успешной barcode validation, если
локальная ресторанная система не нашла профиль. Это on-demand lookup, не
pre-sync.

### 3.6 Admin Support And Audit

Администратор смотрит audit events и inspect view по `correlationId`. Inspect
read-only: он ничего не продлевает, не подтверждает и не восстанавливает.

## 4. Задачи Реализации

### BE-01. Backend Foundation

Добавить базовую внутреннюю структуру `functions`:

- папки `Application`, `Domain`, `Infrastructure`, `Contracts`;
- общий JSON/error response helper;
- единый result/error model для use cases;
- `IClock`;
- options binding для всех backend settings;
- DI registration в `Program.cs`.

Пояснение: это задает границы и уменьшает дублирование в endpoint-ах.

### BE-02. Configuration And Secrets

Описать и подключить typed options:

- storage table names;
- POS HMAC client id/secret/freshness tolerance;
- hashing secrets;
- SMS-Fly settings;
- SMS/barcode/runtime TTL settings;
- Application Insights connection setting.

Пояснение: Functions не должны читать `Environment.GetEnvironmentVariable`
точечно в каждом сервисе. Валидация обязательных настроек должна происходить
при старте или при создании конкретного adapter-а.

### BE-03. Domain Utilities

Реализовать shared domain-компоненты:

- Ukrainian phone normalization and validation;
- questionnaire definition and answer validation;
- `correlationId` generation;
- `phoneRuntimeKey` derivation;
- phone/SMS/barcode hashing;
- SMS code generation;
- barcode generation and parsing;
- POS HMAC canonical string validation.

Пояснение: это ядро правил, которое должно тестироваться отдельно и не зависеть
от Azure SDK.

### BE-04. Storage Adapters

Добавить repository/adapters для таблиц:

- `CustomerProfiles`;
- `DiscountRuntime`;
- `AuditEvents`.

Нужно поддержать:

- point read/write по утвержденным keys;
- insert conflict handling;
- ETag-aware update для barcode consumption;
- continuation token для list endpoints;
- serialization of `AnswersJson` and `MetadataJson`.

Пояснение: storage model уже зафиксирован в `table-storage-design.md`; код
должен повторять его без дополнительных index tables.

### BE-05. Audit Writer And Telemetry Correlation

Сделать общий audit writer и правила записи событий:

- append-only insert в `AuditEvents`;
- `phoneHash` вместо raw phone;
- actor fields: `customer`, `admin`, `pos`, `system`;
- metadata без sensitive payload;
- correlation id в logs/traces, когда он есть.

Пояснение: `AuditEvents` нужен для поддержки и fraud investigation, а App
Insights - для операций. Эти каналы не смешиваем.

### BE-06. Admin Customer Profile API

Реализовать:

```text
POST /api/backoffice/customer-profiles
GET  /api/backoffice/customer-profiles
GET  /api/backoffice/customer-profiles/by-phone/{phone}
PATCH /api/backoffice/customer-profiles/by-phone/{phone}
```

Включить:

- normalize phone;
- validate answers;
- duplicate profile conflict;
- ETag-aware или conflict-safe phone change;
- audit events `customer_profile_created` and `customer_profile_updated`;
- API response shape exactly as in contract.

Пояснение: это первый бизнес-блок, потому что redemption не должен стартовать
без сохраненной анкеты.

### BE-07. Public Start Redemption API

Реализовать:

```text
POST /api/public/redemptions
```

Включить:

- early `correlationId`;
- audit chain for started/invalid_phone/profile_not_found/profile_found;
- lookup in `CustomerProfiles`;
- SMS retry throttle;
- SMS challenge creation;
- SMS-Fly send through abstraction, без local fake SMS provider;
- upsert of `DiscountRuntime` only after profile exists;
- documented error model with `correlationId`.

Пояснение: этот endpoint запускает поддерживаемую цепочку событий даже для
ранних отказов.

### BE-08. Public SMS Verification API

Реализовать:

```text
POST /api/public/redemptions/{redemptionKey}/sms-verifications
```

Включить:

- runtime row lookup;
- SMS expiry and max attempts;
- failed attempt increment;
- successful phone verification;
- previous barcode invalidation by overwrite;
- barcode value generation;
- `BarcodeHash` storage;
- audit events for failed validation, phone verified and barcode issued.

Пояснение: endpoint завершает customer-facing flow и выдает короткоживущий
Code 128 value.

### BE-09. POS Authentication

Реализовать reusable POS HMAC validator:

- headers `x-client-id`, `x-timestamp`, `x-signature`;
- configured client `main-pos-system`;
- freshness tolerance;
- canonical string:

```text
METHOD
PATH
X-TIMESTAMP
SHA256_HEX(BODY)
```

Пояснение: POS routes не защищаются SWA user auth, поэтому HMAC-проверка
является обязательной границей доверия внутри backend.

### BE-10. POS Barcode Validation API

Реализовать:

```text
POST /api/pos/barcodes/validate
```

Включить:

- POS HMAC authentication;
- `scanId` validation;
- barcode parsing;
- runtime lookup;
- invalid/unknown/expired/already_used decisions;
- ETag-based first successful consumption;
- idempotent replay for same `scanId`;
- audit events for requested/failed/succeeded/replay/consumed.

Пояснение: это критичная часть защиты от повторного использования barcode.

### BE-11. POS Customer Profile Lookup API

Реализовать:

```text
POST /api/pos/customer-profiles/lookup
```

Включить:

- POS HMAC authentication;
- phone normalization;
- profile point lookup;
- `found: true` response;
- `404 profile_not_found`;
- audit events `pos_profile_requested`, `pos_profile_returned`,
  `pos_profile_not_found`.

Пояснение: endpoint нужен только для on-demand заполнения POS после успешной
barcode validation.

### BE-12. Admin Audit And Inspect API

Реализовать:

```text
GET  /api/backoffice/audit-events
POST /api/backoffice/redemptions/inspect
```

Включить:

- audit query by `correlationId`;
- optional phone filter through normalized phone hash;
- pagination;
- inspect by `correlationId` or `barcodeValue`;
- read-only status reconstruction from audit/runtime/profile;
- barcode status values from contract.

Пояснение: это support surface. Он помогает разобраться в проблеме, но не
изменяет состояние barcode или redemption.

### BE-13. System Health And Runtime Cleanup

Реализовать:

```text
GET /api/system/health
```

И добавить cleanup mechanism для старых `DiscountRuntime` rows:

- retention based on `DiscountRuntimeRetentionHours`;
- implement cleanup in the first backend MVP pass;
- no sensitive details in health response;
- telemetry for failed storage/SMS dependencies;
- health endpoint usable for availability alert.

Пояснение: health нужен для production-pilot alerts, cleanup - для временных
runtime данных.

### BE-14. Contract Verification And Manual Smoke Scenarios

Подготовить минимальные smoke сценарии:

- create profile;
- start redemption for existing profile;
- profile not found with `correlationId`;
- SMS verification success/failure;
- POS validate success;
- POS idempotent replay;
- POS already used;
- admin inspect by `correlationId`;
- audit list by `correlationId`.

Пояснение: это не заменяет unit tests, но дает понятный ручной путь проверки
интеграции до production-pilot.

### BE-15. Unit Tests

Отдельной финальной задачей добавить unit tests для backend domain/application
logic:

- phone normalization;
- questionnaire validation;
- id generation and barcode parsing;
- HMAC canonical string and signature validation;
- SMS attempt and expiry rules;
- barcode expiry/consumption decisions;
- POS idempotent replay behavior;
- common error mapping;
- audit metadata sanitization.

Пояснение: тесты ставим отдельной завершающей задачей по этому плану, но код
предыдущих задач должен быть написан так, чтобы эти проверки были простыми и
не требовали реального Azure Storage или SMS provider.

## 5. Что Не Входит В Этот План

- Frontend UI implementation, кроме контрактных ожиданий.
- Отдельный Function App за пределами Static Web Apps managed Functions.
- Frontend MSAL setup.
- Managed Identity migration.
- POS nonce replay table.
- Force approve / manager override для barcode.
- Предварительная синхронизация анкет в POS.
- Расчет размера скидки.

## 6. Принятые Решения

На 2026-06-20 зафиксированы такие решения:

1. MVP backend остается в одном `functions` project с внутренними слоями.
2. Local fake SMS provider не добавляем. Redemption flow использует реальную
   SMS-Fly integration, когда она настроена.
3. Runtime cleanup делаем сразу в первом backend MVP pass.
4. Backend implementation tasks выполняются через небольшие feature-branch PR,
   потому что merge в `develop` автоматически деплоит Azure development
   environment.
5. Формат smoke-проверок требует отдельного выбора; варианты описаны ниже.

Git/GitHub стратегия выполнения задач зафиксирована в
`docs/architecture/backend-api-git-pr-strategy.md`.

## 7. Smoke-Test Варианты

Smoke-проверки нужны, чтобы вручную подтвердить, что backend endpoint-ы,
storage side effects и audit chain совпадают с контрактом. Это не unit tests и
не замена автоматическим интеграционным тестам.

Так как fake SMS provider не используется, полная проверка public redemption
до barcode issuance требует реальной отправки SMS и ввода кода, полученного на
телефон. Поэтому smoke-инструмент должен поддерживать ручной шаг для SMS-кода.

### Вариант A. HTTP Collection

Формат: `.http` файл, Postman collection или Bruno collection с готовыми
запросами.

Плюсы:

- удобно запускать по одному endpoint-у;
- легко видеть request/response;
- хорошо подходит для обсуждения API-контракта;
- можно хранить примеры payload рядом с документацией.

Минусы:

- HMAC подпись для POS запросов придется генерировать отдельно или через
  pre-request script, если выбран Postman/Bruno;
- сложнее автоматически переносить значения между шагами, например
  `correlationId`, `redemptionKey`, `barcodeValue`;
- ручная дисциплина важна: легко забыть проверить storage/audit side effects.

Когда выбирать: если главный фокус - прозрачная ручная проверка контрактов и
удобное обсуждение с человеком.

### Вариант B. README Commands

Формат: документ с `curl`/PowerShell командами и пояснениями.

Плюсы:

- не требует отдельного инструмента;
- хорошо работает как onboarding-инструкция;
- явно показывает headers, payload и expected response;
- подходит для Azure и local Functions одинаково, если вынести base URL в
  переменную.

Минусы:

- команды для HMAC будут громоздкими;
- перенос значений между шагами остается ручным;
- на Windows/PowerShell JSON и escaping быстро становятся шумными;
- это хуже масштабируется, если сценариев станет много.

Когда выбирать: если нужен максимально простой, текстовый и независимый от
инструментов smoke checklist.

### Вариант C. Small Smoke Script

Формат: небольшой script, который вызывает API по шагам, хранит промежуточные
значения и печатает результат проверки.

Плюсы:

- может сам переносить `correlationId`, `redemptionKey`, `barcodeValue`;
- может сам подписывать POS HMAC запросы;
- проще повторять один и тот же сценарий после изменений;
- можно явно проверять expected status codes and response fields.

Минусы:

- появляется код поддержки smoke-инструмента;
- из-за реального SMS нужен интерактивный ввод SMS-кода или остановка на шаге
  "enter received code";
- это не должно превратиться в параллельный тестовый фреймворк.

Когда выбирать: если важна повторяемость end-to-end сценариев, особенно POS
HMAC, idempotent replay и audit/inspect chain.

### Рекомендация

Для этого проекта оптимально начать с двух артефактов:

1. `docs/architecture/backend-api-smoke-scenarios.md` - короткий человеческий
   checklist сценариев и ожидаемых результатов.
2. Небольшой smoke script позже в `functions/tools` или `tools`, когда POS HMAC
   и public flow будут реализованы.

HTTP collection можно добавить после стабилизации endpoint-ов, если понадобится
удобный ручной клиент для администратора или внешнего обсуждения с POS стороной.
