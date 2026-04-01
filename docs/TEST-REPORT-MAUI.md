# MAGI.Mobile — Test Report

## Цель

Подтвердить, что мобильный MVP MAGI.Mobile удовлетворяет требованиям КТ-8 и КТ-9:
- приложение построено на .NET MAUI
- использует MVVM
- работает с REST API
- использует локальное SQLite-хранилище
- покрыто unit-тестами и ручными сценариями

## Объект тестирования

| Область | Что проверялось |
|---|---|
| Settings | сохранение URL Gateway, проверка подключения |
| Dashboard | выбор канала, агрегированные метрики, cache/live indicator |
| Services | refresh статусов, команды run/stop, selected channel banner |
| Schedule | загрузка слотов, cache fallback, валидация ввода, add/delete |
| Gallery | загрузка изображений, cache fallback, фильтрация, share |
| Shared layer | validators, services, viewmodels, app state |

## Среда

| Параметр | Значение |
|---|---|
| ОС | Windows 11 |
| .NET SDK | 9/10 SDK installed |
| MAUI target | `net9.0-windows10.0.19041.0` |
| Backend | MAGI ApiGateway |
| Локальная БД клиента | SQLite |

## Автоматические тесты

### Команда

```bash
dotnet test MAGI.Mobile.Tests/MAGI.Mobile.Tests.csproj --no-restore
```

### Покрытые области

| Группа | Что покрывается |
|---|---|
| Validators | URL Gateway, формат даты и времени слота |
| AppState | выбор активного канала |
| ChannelService | API load и cache fallback |
| ImageService | фильтрация и cache fallback |
| ScheduleService | cache fallback и guard-условия |
| DashboardViewModel | persisted channel, cached indicator, banners |
| ServicesViewModel | refresh state, banners, selected channel requirement |
| ScheduleViewModel | cached indicator и last sync |
| GalleryViewModel | cached indicator и filtering |
| SettingsViewModel | load, save, connection check |

## Ручные сценарии

| ID | Сценарий | Ожидаемый результат | Статус |
|---|---|---|---|
| M-01 | Открыть Dashboard при доступном Gateway | отображаются live metrics и выбранный канал | Passed |
| M-02 | Открыть Dashboard без активного канала | показывается warning banner | Passed |
| M-03 | Обновить Services с выбранным каналом | показывается список сервисов и last refresh | Passed |
| M-04 | Нажать Run parser без выбранного канала | показывается ошибка | Passed |
| M-05 | Открыть Schedule при недоступном Gateway после предыдущего sync | отображается cached snapshot | Passed |
| M-06 | Добавить корректный слот | слот появляется после refresh | Passed |
| M-07 | Ввести неверную дату или время слота | операция отклоняется validator-ом | Passed |
| M-08 | Открыть Gallery после sync | отображается список image metadata | Passed |
| M-09 | Отфильтровать Gallery по части имени файла | список сокращается корректно | Passed |
| M-10 | Нажать Share selected | открывается platform share flow | Passed |
| M-11 | Изменить Gateway URL в Settings | значение сохраняется локально | Passed |
| M-12 | Нажать Test connection с валидным URL | показывается успешное сообщение | Passed |

## Найденные и исправленные дефекты

| ID | Дефект | Исправление |
|---|---|---|
| B-01 | Слишком мягкая валидация даты и времени слота | переведена на `TryParseExact` |
| B-02 | Отсутствовал local cache fallback для каналов, расписания и галереи | добавлен SQLite-backed cache |
| B-03 | XAML pages выдавали warnings из-за неcompiled bindings | добавлены `x:DataType` и source binding compilation |
| B-04 | Services screen не показывал operational context | добавлены banner и last refresh |

## Остаточные риски

| Риск | Комментарий |
|---|---|
| Нет полной offline-first синхронизации | для MVP допустимо |
| Нет UI automation поверх MAUI | компенсировано unit-тестами и ручными сценариями |
| Service status не хранится как локальный snapshot | сценарий управления сервисами ориентирован на live backend |

## Вывод

Текущий MAGI.Mobile MVP подходит для учебной защиты:
- есть отдельный MAUI-клиент
- выполнены требования MVVM, REST и локального хранения
- реализованы основные пользовательские сценарии
- подготовлен тестируемый shared-layer и автоматические тесты
- ручные сценарии формализованы для КТ-9
