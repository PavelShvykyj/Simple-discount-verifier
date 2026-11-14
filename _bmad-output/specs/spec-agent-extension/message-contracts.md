# D2 — Контракты обмена

Владелец точных wire/API полей. [Архитектура](../../planning-artifacts/architecture/architecture-agent-extension/ARCHITECTURE-SPINE.md) задаёт состояние, доверие и сроки; [SPEC](SPEC.md) — маршрут чтения. Ни целевой контракт, ни HTTP200 не доказывают реализацию.

## Общие типы

JSON UTF-8, schemaVersion=1; UTC RFC3339 с Z. Duplicate properties, неверные типы/версии и неизвестные поля отклоняются; optional additions допускаются только явным правилом версии. Несовместимость обязательных полей/outcomes/семантики требует major и проверки всех производителей/потребителей; общего runtime-пакета нет.

| Тип | Формат |
|---|---|
| request/event/operation/command ID | UUID D, 36 символов, без повторного использования |
| Opaque ID | Непустые ASCII ≤128, ordinal; Foundry sandbox, MAF session и platform call ID различаются |
| branchId / orderNumber | Строка 1–128 цифр / 1–64 цифр с ведущими нулями; алфавит реальной 1С проверить перед интеграцией |
| amount / amountHint | `{value:"123.45",currency:"UAH"}`; value `[0-9]{1,12}\.[0-9]{2}`, без float; hint не identity/permission |
| Время Connector | `YYYY-MM-DDTHH:mm:ss[.fffffff]Z`, 1–7 знаков дроби при наличии |

## Серверные сообщения

Envelope ≤64 KiB: обязательны `schemaVersion,kind,eventId,requestId,operationId,branchId,issuedAt,expiresAt,route,dataB64`. Command/result добавляют `commandId,toolCallId,commandType` — всего14 полей. Command eventId=commandId; result получает новый eventId/issuedAt, сохраняет остальные correlation/command поля, expiry и route побайтно.

`dataB64` — base64url без padding точных UTF-8 JSON bytes, строгий object без duplicate keys; поле data одновременно запрещено. Start `dataHash` и command `commandDataHash` — SHA256 decoded bytes без повторной сериализации. Binary фото в очередях запрещено, start text ≤8 KiB.

Route — compact JWS: protected header `alg:PS256,kid`, v1 claims; RSA≥2048. Start: `v,iss:"sdv-swa",aud:"sdv-hosted-start",userKey,agentSessionId,branchId,requestId,operationId,issuedAt,expiresAt,dataHash`. Command/result: `iss:"sdv-backend",aud:"sdv-hosted-tool-result"`, те же identity/correlation/times плюс `commandId,toolCallId,commandType,commandDataHash` вместо dataHash. Endpoint/key URL в claims нет. Все дублирующиеся поля сверяются с envelope/control; source queue, подпись, owner и expiry проверяются до effects. Подпись command route не подписывает result outcome: нужны trusted results queue и точное WaitingTool.

| Путь | Тело / ответ |
|---|---|
| POST `/api/backoffice/agent/negotiate` | branchId, optional свой handle → `{schemaVersion:1,sessionHandle,connectUrl}`; проверенный principal/owner, prepare без моделей/1С;401/403 без чужих данных |
| POST `/api/backoffice/agent/turns` | requestId, свой sessionHandle, branchId, text, clientSentAt; фото через контролируемый upload своей session/path, не arbitrary URL |
| SWA → UI | Подтверждённое enqueue →202 `{requestId,operationId,deadline}`; ошибка `{requestId,code,message}`:400format,401/403access,409conflict,413size,503unconfirmed. AcceptanceUnknown запрещает auto retry |
| input → Hosted | kind=`turn.start`, decoded `{text,upload?:{sessionId,path,uploadedAt}}`; start не создаёт missing control |
| Hosted → commands | kind=`tool.command`, commandType=`LookupOrder` или целевой `FiscalizeReceipt`; decoded `{orderNumber,amountHint?}` |
| results → Hosted | kind=`tool.result`, decoded `{outcome,reasonCode,order?}`; только ожидаемые command/tool/branch/owner/session/operation/expiry |
| Hosted → Router | HTTP200 application/json `{schemaVersion:1,eventId,disposition}`; disposition=`Processed\|Duplicate\|Discarded` |
| Hosted → PubSub → UI | `{schemaVersion:1,kind:"turn.final",requestId,operationId,status,answer?,errorCode?}`, status=`Answered\|Failed`; server owner/session group, без route/raw tool output |

Processed завершает этап, включая waiting после одной попытки Send. Только полный ACK исходного eventId разрешает Router Complete: SSE/detached202/disconnect/иной ID не ACK. Conflict решается Hosted CAS, синхронный409 без authoritative check не обещается. Первый terminal финален; late/old request UI игнорирует. Deadline наследуется без продления; issuedAt≤now+5s, issuedAt<expiresAt, интервал≤120s, now<expiresAt. Skew не grace после expiry.

## Текущий API 1С: LookupOrder

Connector обслуживает configured branch, команды последовательно, `application/json`. Чужой branch → Failed/AccessDenied; иной commandType → Failed/InvalidArguments без фискального API. Route имеет3 base64url-сегмента, Connector его не криптографически проверяет и не изменяет. До API проверяются envelope/args/deadline; неправильный формат не разрешает действие.

BaseUri — loopback с возможным publication path; PingPath/LookupPath относительны. Каждый запрос с Basic. GET PingPath + `IBSession:start` получает cookie `ibsession`; следующие запросы несут cookie, `IBSession:finish` закрывает сессию. Перед lookup проверяется текущая session, при необходимости закрывается/создаётся заново. Неудачная подготовка запрещает lookup; текущий business request не повторяется. Весь этап вместе с ping/login ограничен min(30s,остаток−15s).

POST LookupPath: `{contractVersion:1,commandId,branchId,orderNumber,expiresAt,amountHint?}`. Ответ ≤16384 bytes: `{contractVersion:1,outcome,reasonCode,order?,message?}`. Content-Type может отсутствовать; если есть — application/json с optional UTF-8. Message — optional string, не источник бизнес-решения.

| HTTP | outcome / reasonCode | order |
|---|---|---|
| 200 | Found / Success | Обязателен |
| 200 | NotFound / OrderMissing | Запрещён |
| 200 | Failed / ApiUnavailable или ApiOutcomeUnknown | Запрещён |
| 400 | Failed / InvalidArguments или UnsupportedContractVersion | Запрещён; UnsupportedContractVersion нормализуется в InvalidArguments с diagnostic |
| 401/403 | Failed / AccessDenied | Запрещён; не-JSON auth response также нормализуется |
| 408 | Failed / Expired | Запрещён |
| 404/405/413/415 | Failed / InvalidArguments | Запрещён |
| 500/503 | Failed / ApiUnavailable | Запрещён |

Другие сочетания — protocol failure. Order имеет ровно `orderNumber,orderStateCode,receiptState,amount` и optional `receiptNumber`: state=`Open|Closed`, receiptState=`NotFiscalized|Fiscalized|Unknown`; непустой receiptNumber обязателен только при Fiscalized. Args содержат ровно orderNumber и optional amountHint. Amount mismatch блокирует действие, Unknown не разрешает фискализацию.

## Целевая фискализация и отрицательные проверки

FiscalizeReceipt пока не реализован Connector: `Fiscalized/AlreadyFiscalized` требуют подтверждённого receiptNumber; `Rejected/RequiresVerification/TechnicalFailure` закрывают session. Reason catalogue включает Success/AlreadyFiscalized/BusinessRejected/ApiOutcomeUnknown/AccessDenied/Expired/InvalidArguments/ApiUnavailable/OrderMissing; точное API mapping согласуется перед реализацией. Уже фискализированный чек не фискализируется повторно; неизвестный исход после disconnect не разрешает retry. NotFound — успешное отсутствие с возможностью уточнения.

Обязательные negatives: duplicate/unknown fields/version, чужие branch/owner/session, tampered bytes/hash/signature, source-kind mismatch, неожиданные command/tool IDs, expiry, duplicate ID с иным digest, stale result, ACK без полного тела/с чужим ID, неизвестные API outcome/reason. Одинаковая доставка не даёт новых effects; body ID не токен доступа.
