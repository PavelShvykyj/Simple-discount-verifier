# Критерии приёмки

ID сохраняются для плана и проверок. Непроверенное не PASS; приёмка истории не подменяет отдельный AC.

| ID / capability | Проверка и обязательный результат |
|---|---|
| AC-01 / CAP-1 | Номер/сервисный код, одинаковые номера разных branches, ведущие нули: identity=branch+order, сумма hint; актуальные Found/NotFound/Failed, без второго QR. |
| AC-02 / CAP-1,2 | Текст/фото плохого качества, обрезка, неоднозначность и конфликт полей: guard отдельно возвращает допуск и branch/order/amount hints, не выдумывает данные/права; согласованные пороги. |
| AC-03 / CAP-1,5 | Реальный cold intake/upload/enqueue≤45 секунд: access/size/format до приёма, в очереди ссылка; иначе фото исключено, номер доступен, partial upload обнаружим. |
| AC-04 / CAP-2 | Разрешённый/запрещённый/смешанный/неясный ввод, перефразировки, guard outage: до main и отдельно от API; отказ/ошибка закрывает session без main/tools. False admit/deny отдельно; классификация не авторизует запись. |
| AC-05 / CAP-3 | Успех→idle/restart→уточнение: MAF hydration сохраняет branch/order/tool context без старых effects; новый lookup актуален, sandbox ID не MAF ID. |
| AC-06 / CAP-4 | Только явная просьба+runtime args/access/branch/expiry+проверки1С разрешают fiscal tool. Вопрос о возможности и injection в text/photo/result не разрешают действие; после отказа новых tools нет. |
| AC-07 / CAP-4 | Fiscalized/AlreadyFiscalized/Rejected/RequiresVerification/TechnicalFailure интерпретируются по outcome/reasonCode, не message/HTTP200. Неопределённость сохранена, AlreadyFiscalized без действия, UI ответ агента без raw output/выдуманного успеха. |
| AC-08 / CAP-5 | PubSub до Send/requestId,202 после confirmed enqueue, guard/main отдельно. Fast final до202 сопоставим; lost202/ambiguous Send не порождают auto request/второй этап. |
| AC-09 / CAP-5 | Первый UI terminal окончателен; потеря/reconnect/late/duplicate→error≤120 секунд от Send с upload.202 не reset timer; waiting сразу, после10с остаток, known failure раньше. Нет HTTP recovery/изменения нового turn. |
| AC-10 / CAP-6 | Два личных Microsoft/SWA users: owner/session/channel substitution не раскрывает чужие данные. Один login, краткий token своего канала без command publish; server credentials не у browser. |
| AC-11 / CAP-6 | Подмена branch/user/session/operation/expiry/endpoint/version/signature/result→start отвергается до Foundry/effects. Fixed endpoint/query agent_session_id/server identity, UserIdentityImpersonation+app owner checks; call ID не route Connector. |
| AC-12 / CAP-7 | Дубли start/result, два request одной session, несколько instances: одно active turn, create-only/CAS до effects, один owner. Проигравший не вызывает model/command/continuation; UI не шлёт второй turn, local lock недостаточен. |
| AC-13 / CAP-3,7 | Fast result и многошаговый tool cycle: waiting snapshot/IDs/branch/deadline доSend, control+ref одним CAS, без stale post-Send/auto writer. Следующий tool только после успеха в остатке120с с резервом финала/лимитами. |
| AC-14 / CAP-3,5,7 | Crash до/послеCAS/waiting/Send, offline, lost command/result, model/Foundry/Store outage: нет recovery/resend/new tools без state; claimed stage не перезапускается. UI error/timeout, новый запрос/IDs; sent command не отозвана. |
| AC-15 / CAP-7 | Lock loss/lost HTTP ACK/poison/outage: Complete только после terminal ACK обработки/отсечения, не detached Task/202; bounded transport retry/DLQ не business replay. |
| AC-16 / CAP-7 | Expired start до create-only; чужой/late/unexpected result/closed/deleted: все owner/branch/session/operation/tool/command/deadline/status checks; strict update не recreate, нет model/1С; expiry до действия cloud/Connector/1С. |
| AC-17 / CAP-8 | Guard success/deny, crash upload/enqueue/delete, idle/orphans: immediate photo delete/main только поля. Logic App каждые3дня, emergency≤6дней от upload без renew; missed-run/границы/HOME. Sandbox stop/Store TTL не file delete. |
| AC-18 / CAP-8,7 | TTL/delete↔writer и большой/старый snapshot: история3дня от last success без read/telemetry renew; conditional delete не стирает stage. Control/receipt до admission+skew после закрытия; item≤1MB, atomic ref, write TTL renew/read нет, incompatible fail-closed. |
| AC-19 / CAP-8 | Полный известный сценарий и SDK/log errors: нет real texts/answers/photos/payload/envelope/secrets/local sessions/identity; traces только prepared data. AI/MAF/SDK spans без дублей, нужные counters видны; telemetry не обязательна перед эффектом. |
| AC-20 / CAP-1–8 | До приёмки согласованы datasets/пороги/repeats. Отдельно guard/photo/context/tool args/order/results/truth/step-token-time; deterministic+expert, judge не единственное основание. Без дословности/единственного пути/hidden reasoning; regression model/prompt/SDK/context. |
| AC-21 / CAP-4 | Реальная1С: повтор/две sessions/parallel/overlap/обрыв после провайдера не дают второго fiscal effect. Нефискализированный флаг недостаточен; timeout не retry. Только отдельно согласованный безопасный сценарий; без доказательства выпуск закрыт. |
| AC-22 / CAP-1,4,6,7 | Connector own/foreign queue, expiry/API disconnect/Send failure: один branch/base, sequential loopback, свои API sessions, version/branch/expiry доAPI, exact route echo. ReceiveAndDelete без Complete; нет journal/replay. ACL/entity Listen own+Send results без Manage; SCM health не availability. |
| AC-23 / Все | Совместимость wire/state/старого Connector при независимом выпуске. SWA/Hosted/Router workflows из develop и ручной Connector artifact, без лишнего SWA deploy; автоматизация отдельной задачей. |
| AC-24 / CAP-9,8 | Telegram chat+sender allowlist/freshness/replay, независимые listener/API, Busy без очереди, reset API сохраняет manual stop. Status со свежестью; без SCM/business retry. Control не делает lookup/продлевает операцию. Console+daily File/retained10 без secrets/payload/recovery. |

## Gates

T0 — настоящий технический путь SWA→Basic→Router→Hosted/State Store→Connector→тестовое1С окружение→results→та же session/tool→PubSub→UI, без моделей/фискальных effects. Cloud mocks не закрывают интеграцию.

E0 — ранний guard/main lookup на реальных выбранных моделях. E1 — полный агентный gate: два users/branches, lookup/photo (если сохранено), idle-уточнение, явная тестовая фискализация и негативные AC-04–19,22. Для fiscal E1 **реальный провайдер физически недоступен**. Конкретный безопасный тестовый профиль нужно согласовать до его реализации/прогона; удалённый проект API-подстановки не является требованием восстановить его. E1 не доказывает AC-21/23 и не разрешает реальные фискальные действия.

По каждому AC: версии app/contracts/SDK/model/prompt/dataset, условия/repeats, ожидание/наблюдение/счётчики/длительность/cleanup. Без содержимого/секретов. До выпуска также квоты/регион/RBAC/retention/cold path/CPU-RAM/полная стоимость; preview риск не production PASS.
