# MAGI.Mobile — Demo Scenarios

## Сценарий 1. Online Dashboard

1. Запустить ApiGateway.
2. Открыть MAGI.Mobile.
3. Выбрать активный канал на Dashboard.
4. Показать `GatewayStatus`, `PendingSlots`, `UnpostedImages`.

## Сценарий 2. Service Control

1. Перейти на экран Services.
2. Показать banner с текущим каналом.
3. Выполнить `Run parser` или `Run tagger`.
4. Обновить список статусов.

## Сценарий 3. Schedule CRUD

1. Перейти на экран Schedule.
2. Добавить слот с валидной датой и временем.
3. Обновить список.
4. Удалить выбранный слот.

## Сценарий 4. Gallery Filtering

1. Перейти на экран Gallery.
2. Выполнить refresh.
3. Ввести строку поиска.
4. Показать сокращение списка.
5. Выделить изображение и вызвать Share.

## Сценарий 5. Cache Fallback

1. Выполнить refresh Dashboard, Schedule и Gallery при работающем Gateway.
2. Остановить ApiGateway.
3. Снова открыть Schedule и Gallery.
4. Показать `Cached snapshot` и `Last sync`.
