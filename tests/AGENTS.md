# Тесты

Проверяй только затронутый проект, соблюдая инструкции его компонента. Команды Worker/Hosted и Connector — в соответствующих runbooks; connector-tests и worker-client — отдельные xUnit-проекты, connector-client — независимый wire validator с локальным `--self-test`.

Live mode connector-client использует фиксированные namespace/queues/fixtures и потребляет results: сначала проверить профиль/совместимость и отдельно согласовать эксклюзивное ограниченное окно. Флаг approved-exchange не разрешение. Smoke certificate helpers не разрешают Azure mutations. Live/Telegram/SCM/SYSTEM по умолчанию не запускать.

Fixtures только синтетические, local secrets вне Git. Сохранять foreign messages/evidence, без business replay. Локальные подмены/StateReadGuards не доказывают Hosted isolation/CAS/TTL, гарантию1С или приёмку истории.
