# BluePrince 1.3.2 — разбор референса

Источник: предоставленный пользователем Arc059-BluePrince-1.3.2.zip. Выполнено чтение сериализованных объектов через UnityPy; код мода не запускался. Геометрия и текстуры не переносились в FableRealm.

Bundle: UnityFS, Unity 2022.3.62f3. Manifest: REPOLib 3.0.3. Это версия сборки референса, а не требуемая версия для нашей установленной игры.

В LevelContent зарегистрирован 21 модуль: 3 стартовых, 7 обычных, 4 проходных, 4 тупиковых и 3 экстракционных. ModuleAmount в Level равен 8; число вариантов комнат не означает число комнат за один забег.

## Состав иерархий

Счётчики включают вложенные объекты, в том числе отключённые варианты. Это не количество одновременно отрисовываемых объектов и не оценка производительности.

| Модуль | Объекты | MeshRenderer | BoxCollider | MeshCollider | LevelPoint | ValuableVolume |
|---|---:|---:|---:|---:|---:|---:|
| Start Room - MtHolly - Entrance Hall | 2168 | 1345 | 488 | 42 | 9 | 0 |
| Start Room - MtHolly - Secret Garden | 1803 | 1091 | 393 | 17 | 7 | 0 |
| Start Room - MtHolly - Shrine | 1350 | 903 | 237 | 11 | 5 | 0 |
| Module - MtHolly - N - 1 - Archives | 610 | 36 | 468 | 0 | 7 | 61 |
| Module - MtHolly - N - 1 - Boiler Room | 481 | 143 | 206 | 10 | 14 | 29 |
| Module - MtHolly - N - 1 - Cloister | 190 | 42 | 84 | 6 | 9 | 18 |
| Module - MtHolly - N - 1 - Courtyard | 376 | 130 | 148 | 4 | 5 | 29 |
| Module - MtHolly - N - 1 - Den | 384 | 59 | 247 | 5 | 8 | 27 |
| Module - MtHolly - N - 1 - Dining Room | 299 | 38 | 164 | 0 | 8 | 20 |
| Module - MtHolly - N - 1 - Weight Room | 328 | 24 | 184 | 2 | 6 | 29 |
| Module - MtHolly - P - 1 - Rumpus Room | 274 | 34 | 135 | 1 | 6 | 34 |
| Module - MtHolly - P - 1 - Secret Passage | 225 | 28 | 139 | 0 | 3 | 17 |
| Module - MtHolly - P - 1 - Spare Room | 205 | 35 | 100 | 5 | 3 | 16 |
| Module - MtHolly - P - 1 - Workshop | 319 | 36 | 218 | 0 | 4 | 30 |
| Module - MtHolly - DE - 1 - Attic | 311 | 22 | 207 | 3 | 2 | 41 |
| Module - MtHolly - DE - 1 - Pantry | 185 | 16 | 88 | 5 | 2 | 40 |
| Module - MtHolly - DE - 1 - Storage Room | 336 | 17 | 210 | 0 | 2 | 45 |
| Module - MtHolly - E - 1 - Foyer | 311 | 81 | 162 | 0 | 2 | 0 |
| Module - MtHolly - E - 1 - Locksmith | 326 | 96 | 156 | 6 | 2 | 0 |
| Module - MtHolly - E - 1 - Mail Room | 320 | 92 | 186 | 0 | 4 | 0 |
| Module - MtHolly - DE - 2 - Clock Tower | 444 | 111 | 203 | 13 | 8 | 29 |

## Что применять в FableRealm

- Оформлять отдельную архитектурную оболочку: стены, потолок, проёмы и перекрытия, затем мебель и декор.
- Коллизии проектировать отдельно от видимой геометрии; пространство под мебелью проверять реальным персонажем.
- Связывать LevelPoint, NavMeshModifier/NavMeshBox, RoomVolume, ValuableVolume и DirtFinderMap с геометрией каждой комнаты.
- Настраивать варианты стыков через ModulePropSwitch и проверять их отдельно.
- Оценивать библиотеку с камеры персонажа на обоих ярусах, а не только по общей миниатюре.

Наличие компонентов и сохранённых флагов проверки не доказывает работоспособность в нашей версии игры. В этом разборе мод не запускался, навигация и производительность не тестировались.
