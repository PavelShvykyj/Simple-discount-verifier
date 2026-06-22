# Backend API Git And PR Strategy

Дата: 2026-06-20

Этот документ фиксирует стратегию выполнения backend implementation tasks через
Git и GitHub PR.

## 1. Ключевое Правило

`develop` является deploy branch для Azure development environment.

Текущий GitHub Actions workflow
`.github/workflows/azure-static-web-apps-black-pond-085834203.yml` запускается
на каждый push в `develop` и деплоит:

- frontend из `./frontend`;
- managed Functions API из `./functions`;
- frontend build output из `/dist/app/browser`.

Поэтому `develop` нельзя использовать как черновую интеграционную ветку для
незавершенного backend. Каждый merge в `develop` должен быть deploy-safe.

## 2. Рабочая Модель

Backend tasks выполняются через feature branches и PR:

```text
develop
  <- PR from feature/backend-be-01-02-foundation-config
  <- PR from feature/backend-be-03-domain
  <- PR from feature/backend-be-04-05-storage-audit
  <- PR from feature/backend-be-06-admin-profiles
```

Ветка должна содержать ограниченный набор связанных изменений, который можно
локально собрать, проверить и безопасно задеплоить в Azure dev после merge.

## 3. Рекомендуемая Группировка PR

Не делаем один большой PR на весь backend. Backend затрагивает storage, SMS,
HMAC, audit, cleanup и публичные endpoint-ы; большой PR будет сложнее проверить
и откатить.

Рекомендуемый порядок:

| PR | Tasks | Purpose |
| --- | --- | --- |
| PR-1 | BE-01 + BE-02 | Backend foundation, folders, shared response/result model, options/configuration. |
| PR-2 | BE-03 | Domain utilities: phone, questionnaire, ids, hashing, barcode parsing, HMAC canonical rules. |
| PR-3 | BE-04 + BE-05 | Storage adapters for business tables, audit writer, telemetry correlation baseline. |
| PR-4 | BE-06 | Admin customer profile API. |
| PR-5 | BE-07 + BE-08 | Public redemption and SMS verification. |
| PR-6 | BE-09 + BE-10 | POS HMAC authentication and barcode validation. |
| PR-7 | BE-11 + BE-12 | POS profile lookup, admin audit list, admin inspect. |
| PR-8 | BE-13 | Health endpoint and runtime cleanup. |
| PR-9 | BE-14 | Manual smoke scenarios and verification docs/tools. |
| PR-10 | BE-15 | Unit tests. |

Если конкретный PR становится слишком большим, его можно разделить, но нельзя
мержить часть, которая ломает deployed dev environment.

## 4. Локальная Готовность Перед PR

Перед открытием PR нужно проверить минимум:

```powershell
dotnet build functions/SimpleDiscountVerifier.Api.csproj
```

Когда появится test project:

```powershell
dotnet test
```

Если PR меняет frontend, Static Web Apps config или общую сборку, дополнительно
проверяется frontend build. Для чисто backend PR это не обязательно, если
frontend не затронут.

## 5. Готовность Перед Merge В `develop`

Перед merge нужно подтвердить:

- backend project собирается;
- изменения не ломают существующий `POST /api/scanner-survey`;
- реальные секреты не попали в код, документацию, logs или example-файлы;
- если добавлен новый server-side setting, имя добавлено в
  `functions/local.settings.json.example` с безопасным placeholder/default
  значением;
- если deployed code зависит от нового Azure app setting, этот setting уже
  добавлен в Azure Static Web Apps application settings или код безопасно
  не активирует зависимость до настройки;
- storage/table prerequisites известны и совпадают с
  `docs/architecture/table-storage-design.md`;
- API response shape и error model не расходятся с
  `docs/architecture/api-contract.md`.

## 6. Azure Deploy Риск

Так как merge в `develop` сразу деплоит Azure dev, изменения должны быть
deploy-safe.

Безопасный порядок для рискованных блоков:

1. Добавить internal/domain/infrastructure code, который не активируется без
   endpoint вызова.
2. Добавить endpoint с корректной конфигурационной валидацией.
3. Проверить, что Azure dev содержит нужные app settings and secrets.
4. Только потом мержить поток, который реально вызывает внешние зависимости.

Для SMS это особенно важно: fake SMS provider в проекте не используется, значит
public redemption PR должен мержиться только после готовности реальной SMS-Fly
configuration в Azure dev.

Для POS endpoint-ов важно не ослаблять HMAC. `/api/pos/*` открыт как
`anonymous` на SWA edge, поэтому Function-level HMAC validation является
обязательной границей доверия.

## 7. PR Description Template

Каждый PR должен содержать:

```text
Scope:
- какие BE tasks закрывает

Verification:
- какие команды запускались
- какие smoke/manual проверки сделаны

Azure impact:
- нужен ли новый app setting
- нужен ли secret
- влияет ли merge на deployed dev API

Rollback:
- что делать, если после merge Azure dev сломался
```

Если PR требует ручной настройки Azure до merge, это должно быть явно написано
в `Azure impact`.

## 8. PR Portal Checklist

Этот раздел фиксирует, что нужно проверить в Azure Portal до merge и после
автоматического деплоя в `develop`.

Общие portal checks для всех PR:

- До merge: открыть Static Web App `swa-simple-discount-verifier` и убедиться,
  что это development environment, связанный с branch `develop`.
- До merge: если PR добавляет обязательный runtime setting, проверить
  **Settings -> Environment variables** и добавить setting до merge.
- До merge: не заменять существующие connection strings placeholder-значениями.
- После merge: дождаться успешного GitHub Actions workflow
  `Azure Static Web Apps CI/CD`.
- После merge: открыть development Static Web App и проверить, что уже
  существующие публичные/admin страницы не стали недоступны.
- После merge: если PR менял backend runtime, проверить хотя бы один
  затронутый endpoint или безопасный existing endpoint.

### PR-1. BE-01 + BE-02

Before merge:

- Проверить в Azure Static Web Apps application settings наличие базовых
  settings: `AppStorageConnectionString`, `ScannerSurveyTableName`,
  `APPLICATIONINSIGHTS_CONNECTION_STRING`.
- Проверить, что новые planned setting names из
  `functions/local.settings.json.example` уже заведены в Azure dev или код
  допускает их отсутствие до включения конкретных endpoint-ов.
- Если добавляются новые required settings, добавить их в portal до merge.

After deploy:

- Убедиться, что GitHub Actions deploy завершился успешно.
- Проверить, что существующий `POST /api/scanner-survey` не сломан.
- Проверить Application Insights Transaction Search или Live Metrics на
  наличие backend request telemetry, если telemetry уже подключена.

### PR-2. BE-03

Before merge:

- Portal changes обычно не требуются, если PR содержит только domain utilities.
- Проверить, что PR не добавляет новых secrets/settings без обновления
  `functions/local.settings.json.example`.

After deploy:

- Убедиться, что deploy прошел успешно.
- Проверить, что existing `scanner-survey` endpoint работает как раньше.

### PR-3. BE-04 + BE-05

Before merge:

- Проверить Storage Account `sdvstorageaccount`.
- Проверить таблицы: `CustomerProfiles`, `DiscountRuntime`, `AuditEvents`,
  `ScannerSurveyResults`.
- Проверить Azure Static Web Apps settings:
  `CustomerProfilesTableName`, `DiscountRuntimeTableName`,
  `AuditEventsTableName`, `AppStorageConnectionString`.
- Проверить, что `AuditPhoneHashSecret` установлен до включения audit writer
  для новых бизнес endpoint-ов.

After deploy:

- Убедиться, что deploy прошел успешно.
- Проверить existing `scanner-survey`.
- Проверить Application Insights на отсутствие backend exceptions от storage
  configuration.
- Если в PR есть безопасный diagnostic/manual path для storage adapters,
  выполнить его и убедиться, что таблицы доступны.

### PR-4. BE-06

Before merge:

- Проверить route rule `/api/backoffice/*` в
  `frontend/public/staticwebapp.config.json` и в deployed Static Web Apps
  behavior.
- Проверить, что в Static Web Apps Role Management есть пользователь с custom
  role `admin` для ручной проверки admin API.
- Проверить settings/table prerequisites для `CustomerProfiles` and
  `AuditEvents`.

After deploy:

- Под admin session проверить create/get/list/update customer profile.
- Проверить, что не-admin/anonymous доступ к `/api/backoffice/*` запрещен SWA.
- Проверить строки в `CustomerProfiles`.
- Проверить audit event для create/update в `AuditEvents`.

### PR-5. BE-07 + BE-08

Before merge:

- Проверить SMS-Fly settings: `SmsFlyApiKey`, `SmsFlySender`,
  `SmsCodeTtlSeconds`, `SmsRetryAfterSeconds`.
- Проверить hashing settings: `PhoneRuntimeKeySecret`, `SmsCodeHashSecret`,
  `BarcodeHashSecret`, `AuditPhoneHashSecret`.
- Проверить `BarcodeTtlSeconds`.
- Проверить, что в Azure dev есть тестовый customer profile и реальный телефон
  для SMS smoke check.
- Так как fake SMS provider не используется, не мержить этот PR, пока реальная
  SMS-Fly configuration не готова.

After deploy:

- Запустить public start redemption для существующего профиля.
- Подтвердить, что SMS приходит на тестовый телефон.
- Ввести SMS code и получить `barcodeValue`.
- Проверить `DiscountRuntime`: runtime row создан/обновлен, raw SMS code and
  raw barcode не сохранены.
- Проверить `AuditEvents`: `redemption_started`, `profile_found`,
  `sms_sent`, `phone_verified`, `barcode_issued`.
- Проверить negative scenario `profile_not_found` с `correlationId`.

### PR-6. BE-09 + BE-10

Before merge:

- Проверить route rule `/api/pos/*` позволяет `anonymous` на SWA edge.
- Проверить POS settings: `PosMainClientId`,
  `PosMainClientHmacSecret`, `PosRequestFreshnessToleranceSeconds`.
- Проверить, что POS HMAC secret в Azure dev совпадает с локальным secret,
  используемым smoke client/script, но не попадает в git или chat/logs.
- Проверить prerequisites для `DiscountRuntime`, `BarcodeHashSecret`,
  `AuditEvents`.

After deploy:

- С валидным HMAC проверить успешный `POST /api/pos/barcodes/validate`.
- Повторить тот же request с тем же `scanId` и получить
  `idempotentReplay: true`.
- Повторить barcode с другим `scanId` и получить `already_used`.
- Проверить invalid HMAC возвращает `401 unauthorized`.
- Проверить `AuditEvents` для requested/succeeded/replay/failed/consumed
  событий.

### PR-7. BE-11 + BE-12

Before merge:

- Проверить POS HMAC settings остаются настроенными.
- Проверить admin role для `/api/backoffice/*`.
- Проверить наличие `CustomerProfiles`, `DiscountRuntime`, `AuditEvents`.
- Подготовить `correlationId` тестового redemption flow для inspect smoke.

After deploy:

- С валидным POS HMAC проверить
  `POST /api/pos/customer-profiles/lookup` для существующего телефона.
- Проверить not-found lookup.
- Под admin session проверить `GET /api/backoffice/audit-events` by
  `correlationId`.
- Под admin session проверить `POST /api/backoffice/redemptions/inspect` by
  `correlationId`.
- Проверить, что inspect endpoint read-only и не меняет barcode/runtime state.

### PR-8. BE-13

Before merge:

- Проверить `DiscountRuntimeRetentionHours`.
- Проверить, что Application Insights resource подключен через
  `APPLICATIONINSIGHTS_CONNECTION_STRING`.
- Если cleanup реализован timer trigger-ом, проверить, что Functions hosting
  settings позволяют timer execution в SWA managed Functions.
- Проверить, что health endpoint не требует secrets and не раскрывает
  connection strings.

After deploy:

- Проверить `GET /api/system/health`.
- Проверить Application Insights availability/transaction telemetry для health.
- Проверить cleanup вручную на тестовых expired runtime rows или через
  безопасный test scenario, если он предусмотрен.
- Проверить отсутствие sensitive data в traces/exceptions.

### PR-9. BE-14

Before merge:

- Проверить, что smoke scenarios не содержат real secrets, real SMS codes,
  raw production phone data или connection strings.
- Проверить, что команды/скрипты явно используют placeholders or local secure
  variables.

After deploy:

- Выполнить smoke scenarios против Azure dev.
- Сохранить в `backend-api-implementation-status.md` evidence: какие сценарии
  прошли, дата, environment, PR/commit.
- Если smoke script добавлен, проверить, что он не логирует secrets and raw
  sensitive payload.

### PR-10. BE-15

Before merge:

- Portal changes обычно не требуются.
- Проверить, что tests не зависят от real Azure Storage, real SMS-Fly или real
  portal secrets.

After deploy:

- Убедиться, что GitHub Actions deploy прошел успешно.
- Проверить, что test additions не изменили runtime behavior.
- Обновить status evidence командой `dotnet test`.

## 9. Status Documentation Rule

После merge каждого PR обновляется
`docs/architecture/backend-api-implementation-status.md`:

- task status переводится по цепочке `todo` -> `in-progress` -> `review` ->
  `done`;
- в `Evidence` добавляется build/test/smoke evidence или ссылка на PR/commit;
- unresolved Azure prerequisites остаются в notes или blockers.

Status-документ обновляется вместе с кодом, а не после завершения всего backend.

## 10. Что Не Делаем

- Не мержим незавершенный большой backend в `develop`.
- Не используем `develop` как экспериментальную ветку.
- Не коммитим реальные secrets.
- Не добавляем временные bypass-ы для Azure auth или POS HMAC.
- Не деплоим public redemption с SMS-Fly, если Azure dev settings еще не
  готовы.
- Не смешиваем unrelated frontend refactoring с backend PR.

## 11. Рекомендация

Использовать PR-группировку из раздела 3 и считать каждый merge в `develop`
маленьким production-like deploy в development Azure environment.

Для backend это означает: PR должен быть либо полностью рабочим, либо
нейтральным для уже задеплоенного приложения.
