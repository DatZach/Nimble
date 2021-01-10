# Nimble

This is a collection of patches for GameMaker: Studio 2's `GMAssetCompiler.exe` that improve first-time and from-cache compile times. Patches are done at runtime via [Harmony](https://github.com/pardeike/Harmony) so that there are no files to replace on disk. Patches have been tested and work for: `2.2.5.481`, and `2.3.1.542`.

## Getting Started
In order to run this code you will need `IdeMagic`, a launcher for GameMaker: Studio 2 which enables the loading of third-party plugins for the IDE and Compiler. IdeMagic is currently not released and may not see one, we'll see. For now, this is left as an exercise to the end-user to figure out how to get this code running.

## Patch Overview
* **TexturePageEntry Cache** - Save all TPEs in a single BIN file to improve filesystem performance. *From-cache build time improved.*
* **TexturePage Packing Algorithm** - Simplier algorithm for packing tpages. Packed sheets are lossier but are packed significantly faster. Good as an option for in-dev, allowing for the default algorithm when building for release. *First-time and from-cache (when sprites changed) build time improved.*
* **GMLCompiler String/Asset ID Caching** - Replace finding IDs for strings and resources with a Dictionary for O(log n) search. *First-time and from-cache build time improved*

## Results
Vanilla First-Time Build
```
Stats : GMA : Elapsed=937056.4675 (15 minutes 36 seconds)
Stats : GMA : sp=10013,au=300,bk=1,pt=8,sc=3109,sh=15,fo=4,tl=0,ob=156,ro=29,da=302,ex=6,ma=1381
```

Vanilla From-Cache Build
```
Stats : GMA : Elapsed=98274.8128 (1 minute 37 seconds)
Stats : GMA : sp=10013,au=300,bk=1,pt=8,sc=3109,sh=15,fo=4,tl=0,ob=156,ro=29,da=302,ex=6,ma=1381
```

Nimble First-Time Build
```
Stats : GMA : Elapsed=223650.7341 (3 minutes 43 seconds)
Stats : GMA : sp=10013,au=300,bk=1,pt=8,sc=3109,sh=15,fo=4,tl=0,ob=156,ro=29,da=302,ex=6,ma=1381
```

Nimble From-Cache Build
```
Stats : GMA : Elapsed=36417.3769 (36 seconds)
Stats : GMA : sp=10013,au=300,bk=1,pt=8,sc=3109,sh=15,fo=4,tl=0,ob=156,ro=29,da=302,ex=6,ma=1381
```

## Authors

* **Zach Reedy** - *Primary developer* - [DatZach](https://github.com/DatZach)

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details. Explicitly, this repository exists to give YoYoGames Ltd. permission to integrate these patches into their software if desired. I only ask to be credited.