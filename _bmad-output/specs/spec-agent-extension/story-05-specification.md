# История5: завершить основу Hosted Worker

Spec checkpoint пройден; done checkpoint открыт. Локальная основа/298 проверок и Hosted signed session.prepare A/B подтверждены; это не CAS/TTL/isolation/MAF/E2E PASS. Cloud turn.start/tool.result закрыты до доказанной границы доверия источника.

## Задача и границы

Server-only develop LookupOrder: prepare→start→одна command→точный result→один turn.final собственному подписчику. Без models/guard/photo/fiscalization, Router/SWA/UI реализации, maintenance workflow, production. Технический профиль — фиксированный allowlist входов/отдельный signer/caller, не браузерный переключатель. Источник тестовых данных и сценарий с текущим Connector согласуются до live; удалённая API3-подстановка не восстанавливается автоматически.

Форматы — [message-contracts](message-contracts.md); state/deadline/identity — [архитектура](../../planning-artifacts/architecture/architecture-agent-extension/ARCHITECTURE-SPINE.md), разделы «Состояние», «Отказы», «Доверие». Исполнение — [worker-runbook](worker-runbook.md). Контракты здесь повторно не описываются.

## Оставшаяся работа

- Доказать trusted originating/delegated context и source/bytes результата; delegated user недостаточен. Пока не доказано — start/result остаются закрыты.
- На реальном Store проверить user isolation A/B, create-only, specific/stale/missing ETag/CAS/delete, TTL/read-not-renew, snapshot hydration новым экземпляром/после idle. Не менять рабочие TTL ради теста.
- Проверить реальные AMQP metadata/TTL и число попыток AzureTransport, fast-result и конфликт двух starts; без post-Send writer.
- Подтвердить real PubSub group isolation/delivery/expiry и отсутствие content/secrets/duplicate spans в Hosted telemetry.
- Недоступные управляемые crash-точки отметить непроверенными, сопоставив с локальной матрицей; не заменять cloud доказательства fallback.

| ID | Обязательный результат |
|---|---|
| S5-AC01 | Restore/build/linux-x64 flat ZIP/SHA; опубликована и вызвана именно проверенная active version с правильным query selector и конечным JSON ACK |
| S5-AC02 | Два user/branches: prepare и все подмены owner/session/branch/signature/identity/source/expiry; originating service доказан, чужие данные/эффекты недоступны |
| S5-AC03 | Совместимый Connector получает одну команду с точными D2/AMQP полями; результат совпадает с expected waiting, исход1С отображён без выдуманного успеха; независимые счётчики |
| S5-AC04 | Реальные Store create-only/CAS/delete/version/current snapshotRef/TTL и MAF hydration; incompatible/missing/oversize fail-closed |
| S5-AC05 | Concurrent starts/results, changed digest, второй eventId того же tool, fast result/late/closed/deleted/protectUntil: максимум один Send/result claim/final, новый этап не затёрт |
| S5-AC06 | Store outage/ambiguous write/crash до-после receipt/CAS/waiting/Send/result/final: нет replay/второго эффекта; недоступные Hosted fault-точки явно выделены |
| S5-AC07 | Подписчик подключён заранее, один correlated turn.final только owner; потеря доставки не recovery. Проверены whitelist и отсутствие raw payload/route/identity/tokens/connect URLs в logs/traces |

До окна отдельно согласовать identities/roles/ресурсы/версию, ограничения вызовов/времени и расходы (включая Store/SB/WPS/idle), остановку/cleanup. Prerequisites/capacity не равны runtime PASS. Следующая история6 — после явной приёмки5, без автоматического deployment или выдачи прав.
