# D2 — Контракты обмена

Владелец точных wire/API полей. [Архитектура](../../planning-artifacts/architecture/architecture-agent-extension/ARCHITECTURE-SPINE.md) задаёт состояние, доверие и сроки; [SPEC](SPEC.md) — маршрут чтения. Ни целевой контракт, ни HTTP200 не доказывают реализацию.

## Общие типы

JSON UTF-8; input/ACK/final schemaVersion=1, command/result v2 описаны ниже; UTC RFC3339 с Z. Duplicate properties, неверные типы/версии и неизвестные поля отклоняются; optional additions допускаются только явным правилом версии. Несовместимость обязательных полей/outcomes/семантики требует major и проверки всех производителей/потребителей; общего runtime-пакета нет.

| Тип | Формат |
|---|---|
| request/event/operation/command ID | UUID D, 36 символов, без повторного использования |
| Opaque ID | Непустые ASCII ≤128, ordinal; Foundry sandbox, MAF session и platform call ID различаются |
| branchId / orderNumber | Строка 1–128 цифр / 1–64 цифр с ведущими нулями; алфавит реальной 1С проверить перед интеграцией |
| amount / amountHint | `{value:"123.45",currency:"UAH"}`; value `[0-9]{1,12}\.[0-9]{2}`, без float; hint не identity/permission |
| Время Connector | `YYYY-MM-DDTHH:mm:ss[.fffffff]Z`, 1–7 знаков дроби при наличии |

## Серверные сообщения

Envelope ≤64 KiB: общие поля `schemaVersion,kind,eventId,requestId,operationId,branchId,issuedAt,expiresAt,route,dataB64`. Command/result добавляют `commandId,toolCallId,commandType`. Command eventId=commandId; result получает новый eventId/issuedAt, сохраняет correlation/command поля, expiry и route побайтно.

Версии на границах:
- SWA→Router→Worker: prepare/start v1 (10 полей), decoded prepare `{}`, start `{text,upload?:{sessionId,path,uploadedAt}}`. Текущий S5 без моделей допускает только `{text:"<номер заказа>"}` (1–64 цифры); это валидация LookupOrder, не список fixtures.
- Worker→Connector: command v2 (14 полей), дополнительные к общим+tool `commandDataHash` = lowercase hex SHA256 точных decoded command bytes. Connector проверяет hash до API. Новый Connector принимает также старый v1 без этого поля, но новый Worker выпускает только v2.
- Connector→results→Router: result v2 (14 полей), Connector сохраняет `commandDataHash` исходной команды, заменяет только kind/eventId/issuedAt/dataB64. Для старого command v1 возвращает result v1; они не допускаются новым Worker.
- Router→Worker: result v2 + обязательный `resultProof` (15 полей). Router добавляет proof только после чтения доверенной results queue; v2 без proof Worker отвергает. Другие source queues не могут стать Result по kind тела.

`dataB64` — base64url без padding точных UTF-8 JSON bytes, строгий object без duplicate keys; поле data одновременно запрещено. Start `dataHash`, command `commandDataHash`, result `resultDataHash` — SHA256 decoded bytes без повторной сериализации. Binary фото в очередях запрещено, start text ≤8 KiB (текущий S5 дополнительно ограничен номером заказа).

Подписи — compact JWS, protected header ровно `alg:PS256,kid`, v=1 claims; RSA≥2048, без endpoint/key URL. SWA имеет private key, Worker — только public verifier. Prepare claims: `v,iss:"sdv-swa",aud:"sdv-hosted-prepare",userKey,agentSessionId,branchId,requestId,operationId,issuedAt,expiresAt,dataHash`. Start имеет тот же набор с `aud:"sdv-hosted-start"`.

Command route — исходный проверенный SWA start JWS, сохранённый побайтно. Он связывает исходный запрос/owner/session/branch, но не подписывает новые command fields или outcome. Worker не имеет private signing key. Доверие к команде обеспечивается разрешённым Sender очереди commands, входными проверками Connector и полным WaitingTool до Send.

`resultProof` подписывает Router отдельным private key, `iss:"sdv-router",aud:"sdv-hosted-tool-result"`. Claims ровно: `v,iss,aud,userKey,agentSessionId,branchId,requestId,operationId,issuedAt,expiresAt,eventId,commandId,toolCallId,commandType,commandDataHash,resultDataHash,routeHash`. `routeHash` — SHA256 UTF-8 bytes исходного route. Все дубли полей побайтно совпадают с envelope; resultDataHash защищает именно bytes результата, commandDataHash — сохранённую привязку к команде. issuedAt — время результата, expiresAt наследуется без продления и сверяется с WaitingTool; исходный deadline ограничен start/command, отдельного продления по времени подписи нет.

Перед делегацией owner Router проверяет исходную подпись SWA и совпадение identity/correlation/expiry route с результатом; подпись Router выпускает только после source admission results queue. Worker проверяет Router public kid/issuer/audience/result bytes/route hash до state/effects, затем точные owner/session/branch/request/operation/command/tool/route/commandDataHash/expiry из WaitingTool. Подпись SWA, подпись Router, queue RBAC и WaitingTool дополняют друг друга.

Это несовместимая версия command/result; перед новым окном нужны Connector v2 и Router с proof. Старый Connector v1 новые команды отвергает. Нет миграции активных waiting, fallback к старой подписи или replay. Фактическая готовность — в [STATUS](../../../agent-extension/STATUS.md).

| Путь | Тело / ответ |
|---|---|
| POST `/api/backoffice/agent/negotiate` | branchId, optional свой handle → `{schemaVersion:1,sessionHandle,connectUrl}`; проверенный principal/owner, prepare без моделей/1С;401/403 без чужих данных |
| POST `/api/backoffice/agent/turns` | requestId, свой sessionHandle, branchId, text, clientSentAt; фото через контролируемый upload своей session/path, не arbitrary URL |
| SWA → UI | Подтверждённое enqueue →202 `{requestId,operationId,deadline}`; ошибка `{requestId,code,message}`:400format,401/403access,409conflict,413size,503unconfirmed. AcceptanceUnknown запрещает auto retry |
| input → Router → Hosted | `turn.start` по матрице версий выше. S5 принимает `{text}`; upload относится к целевому профилю. Start не создаёт отсутствующий control |
| Hosted → commands | S5: `tool.command`, `commandType=LookupOrder`, decoded `{orderNumber}`. `FiscalizeReceipt` и `amountHint` относятся к целевому профилю |
| results → Router → Hosted | `tool.result`, decoded `{outcome,reasonCode,order?}`. Router добавляет `resultProof`. Worker сверяет результат с сохранённым ожиданием |
| Hosted → Router | HTTP200 application/json `{schemaVersion:1,eventId,disposition}`; disposition=`Processed\|Duplicate\|Discarded` |
| Hosted → PubSub → UI | Общие поля `schemaVersion:1,kind:"turn.final",requestId,operationId,status`. `Answered` содержит только `answer`, `Failed` — только `errorCode` из двух полей результата. Публикация в server owner/session group без route и raw tool output |

Без ACK возвращаются `400` для неразбираемого JSON или недопустимого eventId, `403` для несовпавшего контекста, `413` для превышения лимита, `415` для неподдерживаемого Content-Type. Ошибки обработки: `504` для таймаута, `503` для дошедшего до обработчика ответа SDK 429/503, `500` для прочих внутренних ошибок. Отказы и неопределённые исходы Send/Publish диагностируются отдельно и сами по себе не запрещают ACK этапа.

`Processed` подтверждает завершение этапа. Он возможен после записи `WaitingTool`, даже если истёкший дедлайн исключил Send. Сам ACK не подтверждает Send, выполнение 1С или доставку подписчику и не разрешает повтор эффекта. Только полный ACK исходного eventId разрешает Router Complete: SSE/detached202/disconnect/иной ID не ACK. Conflict решается Hosted CAS, синхронный409 без authoritative check не обещается. Первый terminal финален; late/old request UI игнорирует. Deadline наследуется без продления; issuedAt≤now+5s, issuedAt<expiresAt, now<expiresAt; интервал start/command≤120s. Result наследует исходный deadline и сверяется с WaitingTool. Skew не grace после expiry.

### Группа Web PubSub для turn.final

Сервер выдаёт браузеру подключение к той же группе, в которую публикует Worker. Вход алгоритма — пара `[userKey, agentSessionId]` в этом порядке, без нормализации или изменения регистра. Сериализовать её как компактный JSON-массив строк в UTF-8 без BOM через `System.Text.Json` с `JsonSerializerDefaults.Web` и стандартным экранированием `JavaScriptEncoder.Default`. Имя группы — префикс `sdv-worker-` и SHA-256 этих байтов в нижнем hex. Сервис выдачи подключения вычисляет имя из проверенных значений владельца и сессии и разрешает клиенту доступ только к его группе.

| userKey | Точные байты UTF-8 при agentSessionId = sandbox-a | Имя группы |
|---|---|---|
| `sdv:test:alice` | `["sdv:test:alice","sandbox-a"]` | `sdv-worker-c71e04710c72032b08cb7da273ab24b5d28aec47bb7db20cf6c0b61897bb296e` |
| `sdv:test:alice&bob` | `["sdv:test:alice\u0026bob","sandbox-a"]` | `sdv-worker-15f50b529cb666b90666561f5ffd5f7373993888de185816cb8d487703bda43e` |

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
