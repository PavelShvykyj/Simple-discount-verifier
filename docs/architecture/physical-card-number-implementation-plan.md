# Physical Discount Card Number: Implementation Plan And Status

Дата: 2026-07-28

Документ фиксирует согласованный план сквозного добавления номера физической
дисконтной карты в анкету клиента и текущее состояние реализации.

Связанные источники истины:

- `docs/product/discount-verification-summary.md`
- `docs/architecture/api-contract.md`
- `docs/architecture/table-storage-design.md`
- `docs/architecture/architecture-decisions.md`
- `docs/process/pos-discount-business-process.md`
- `specs/001-discount-verification/spec.md`

## Согласованные решения

| Решение | Значение |
| --- | --- |
| Имя поля API/storage mapping | `physicalCardNumber` / `PhysicalCardNumber` |
| Место в профиле | Отдельное системное поле верхнего уровня рядом с `phone`, не элемент `answers[]` |
| Обязательность | Обязательно при создании и обновлении анкеты |
| Формат | Строка из 13 цифр с корректной контрольной цифрой EAN-13 |
| Уникальность | Логически ожидается, но backend и Azure Table Storage ее не контролируют |
| Редактирование | Администратор может изменить номер карты |
| Роль телефона | Не меняется: идентификатор профиля, SMS и POS-поиск локальной карты |
| FE-ввод | Ручной ввод и сканирование камерой мобильного браузера |
| POS-создание | EAN-13 записывается в `КодКарты` и `РучнойКод` |
| POS-обновление | Не выполняется автоматически; POS может явно повторить существующий profile lookup и обновить локальную карту |
| API | Новые маршруты не добавляются; расширяются существующие DTO |
| Azure | Новые таблицы, индексы и Bicep-ресурсы не добавляются |
| Исходные данные | Текущая таблица анкет очищается и заполняется заново после готовности реализации |

## Статусы

| Status | Meaning |
| --- | --- |
| `todo` | Задача еще не начата. |
| `in-progress` | Задача реализуется в текущей сессии. |
| `blocked` | Есть конкретный внешний блокер. |
| `review` | Реализация готова, требуется проверка. |
| `done` | Реализация завершена и подтверждена указанными evidence. |

## Общий статус

| Field | Value |
| --- | --- |
| Current phase | PCN-02 завершен; следующий пункт — PCN-03 |
| Backend/API | `done` |
| Importer | `done` |
| Frontend | `todo` |
| POS/1C | `todo` |
| Azure reset/reimport | `todo` |

## Task Tracker

| ID | Task | Status | Dependencies | Evidence |
| --- | --- | --- | --- | --- |
| PCN-00 | Зафиксировать требования и обновить действующие документы | `done` | none | Этот документ и связанные документы, обновленные 2026-07-28 |
| PCN-01 | Backend, storage и существующие API | `done` | PCN-00 | 2026-07-28: backend build — 0 warnings/errors; EAN-13 check и in-memory smoke для create/read/list/update, invalid values, storage round-trip, support inspect, POS lookup, JSON и audit — passed |
| PCN-02 | JSON CustomerProfileImporter | `done` | PCN-01 shared validation/storage contract | 2026-07-28: build — 0 warnings/errors; sample dry-run — 1 valid, 2 skipped; `vopac.json` dry-run — 73 total, 69 valid, 4 skipped, 0 writes |
| PCN-03 | FE: ручной ввод и камера | `todo` | PCN-01 API contract | |
| PCN-04 | POS/1C: создание и явное обновление карты | `todo` | PCN-01 POS response contract | |
| PCN-05 | Очистка, повторный импорт и end-to-end проверка | `todo` | PCN-01..PCN-04 | |

## PCN-01. Backend, storage и API

- Добавить общий domain-валидатор EAN-13.
- Добавить `physicalCardNumber` в профиль, admin create/update/read/list,
  support inspect и ответ `POST /api/pos/customer-profiles/lookup`.
- Хранить строку в свойстве `PhysicalCardNumber` существующей строки
  `CustomerProfiles`.
- Не менять ключ профиля: `PartitionKey = phone`,
  `RowKey = <normalized phone without +>`.
- Не добавлять проверку уникальности номера карты.
- Не писать raw `physicalCardNumber` в audit metadata и Application Insights.
- Не добавлять новые API routes или Azure-ресурсы.

Проверка:

- backend build;
- создание, чтение и изменение профиля;
- отклонение отсутствующего, нецифрового и checksum-invalid EAN-13;
- POS lookup возвращает сохраненный `physicalCardNumber`.

## PCN-02. CustomerProfileImporter

- Разрешить mapping произвольного исходного ключа в системную цель
  `physicalCardNumber`.
- Не помещать значение в `answers[]`.
- Использовать тот же EAN-13-валидатор, что и backend.
- Добавить `PhysicalCardNumber` в создаваемую Azure Table entity.
- Обновить sample input, sample mapping, POS mapping и README.
- Сохранять текущее поведение: невалидная строка сообщается и пропускается.

Проверка:

- build;
- dry-run с валидным и невалидным EAN-13;
- dry-run на согласованном JSON-формате до любой реальной загрузки.

## PCN-03. Frontend

- Добавить отдельный обязательный control рядом с телефоном.
- Поддержать ручной ввод 13 цифр на мобильной цифровой клавиатуре.
- Добавить действие сканирования EAN-13 камерой мобильного браузера, повторно
  используя установленный ZXing.
- Не добавлять API для сканирования: распознавание выполняется в браузере.
- Передавать `physicalCardNumber` в существующие create/update endpoints.
- Разрешить изменение номера при редактировании анкеты.

Проверка:

- component/API tests;
- lint и build;
- ручной ввод валидного и невалидного EAN-13;
- сканирование реальной физической карты на целевом мобильном браузере;
- светлая/темная тема, разрешение и отказ камеры, повторное сканирование.

## PCN-04. POS/1C

- Оставить поиск локальной карты по телефону через `АнкетныеДанные`.
- При отсутствии карты получить существующим profile lookup поле
  `profile.physicalCardNumber`.
- При создании записать EAN-13 в `КодКарты` и `РучнойКод`.
- Не создавать пункт анкеты для `physicalCardNumber`.
- Для явного обновления повторно вызвать существующий profile lookup и обновить
  оба реквизита локальной карты.
- Не добавлять новый endpoint и не выполнять автоматическую синхронизацию.

Проверка:

- компиляция BSL в целевой конфигурации;
- создание новой карты;
- повторный поиск той же карты по телефону;
- изменение номера в web-профиле и отдельное явное обновление POS;
- подтверждение, что обычный быстрый POS-путь ничего не синхронизирует.

## PCN-05. Ввод в эксплуатацию

1. Завершить и проверить PCN-01..PCN-04.
2. Остановить создание/редактирование анкет на время переключения.
3. Очистить текущие строки `CustomerProfiles`.
4. Выполнить dry-run полного JSON.
5. Повторно импортировать анкеты с `physicalCardNumber`.
6. Выполнить end-to-end сценарий FE -> Azure Table -> POS lookup -> создание
   локальной карты.

Очистка и реальная загрузка данных выполняются только по отдельной команде
пользователя.

## Рекомендуемое разбиение по сессиям

1. PCN-01 + PCN-02: backend и importer используют общий контракт и валидатор.
2. PCN-03: frontend и проверка камеры на мобильном устройстве.
3. PCN-04: POS/1C и проверка в целевой конфигурации.
4. PCN-05: контролируемая очистка, импорт и end-to-end проверка.

## Правила обновления статуса

- В начале сессии переводить только выполняемую задачу в `in-progress`.
- `review` и `done` требуют фактического evidence: build/test/dry-run,
  результат API, проверка браузера или целевой 1С.
- Проверку документа или исходного кода не считать подтверждением поведения
  мобильной камеры, Azure runtime или POS/1С.
- При изменении решения сначала обновить раздел «Согласованные решения», затем
  зависимые контракты и задачи.
