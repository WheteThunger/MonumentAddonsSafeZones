## Features

- Allows placing safe zones at monuments via the Monument Addons plugin
- Allows placing spherical safe zones as well as box safe zones
- Allows configuring safe zone size (for safe zones created by this plugin)
- Automatically recreates safe zones at configured monuments on restarts and wipes, as a feature of Monument Addons

## Required dependencies

- [Monument Addons](https://umod.org/plugins/monument-addons)

## Optional tools

- [Telekinesis](https://umod.org/plugins/telekinesis) -- Allows moving and rotating safe zones placed by this plugin.

## How it works

This plugin registers a custom addon called `safezone` with Monument Addons. This addon can be placed via the `maspawn` command, like `maspawn safezone`. Safe zones placed this way will be saved in your currently selected Monument Addons profile, allowing Monument Addons to recreate the safe zones when the server restarts or wipes. For example, you can add a safe zone to the Ferry Terminal monument, and it will always be at the correct place within that monument, even after switching maps.

## Permissions

- `monumentaddons.admin` (from MonumentAddons) -- Allows creating safe zones, and editing/moving/removing safe zones created by this plugin.
- `telekinesis.admin` (from Telekinesis) -- Allows moving safe zones created by this plugin.

## Commands

- `maspawn safezone offset <x>,<y>,<z> size <x>,<y>,<z> radius <number>` -- Creates a safe zone where you are aiming. The `offset`, `size` and `radius` options are all optional and can be provided in any order.
  - Example: `maspawn safezone radius 10` -- Creates a spherical safe zone with 10m radius (20m diameter), centered on the point where you are aiming.
  - Example: `maspawn safezone radius 10 offset 0,5,0` -- Creates a spherical safe zone, centered 5m above the point where you are aiming.
  - Example: `maspawn safezone size 20,10,20` -- Creates a box safe zone that is 20m wide and 10m tall, centered on the point where you are aiming.
  - Example: `maspawn safezone size 20,10,20 offset 0,5,0` -- Creates a box safe zone that is 20m wide and 10m tall, centered 5m above the point where you are aiming.
- `maedit safezone offset <x>,<y>,<z> size <x>,<y>,<z> radius <number>` -- Edits the safe zone you are aiming at. Same options as `maspawn safezone`.

Note: When providing `size` or `offset` options, make sure there are **NO SPACES** in between the numbers.

- **Wrong**: `offset 0, 10, 0`
- **Correct**: `offset 0,10,0`

## Getting started

### Create a spherical safe zone

1. Aim at the ground in a monument, approximately where you want the center of the safe zone to be.
2. Run the command `maspawn radius 10` to create a spherical safe zone with 10m radius (20m diameter).

### Create a box safe zone

1. Aim at the ground in a monument, approximately where you want the center of the safe zone to be.
2. Run the command `maspawn safezone size 10,10,10 offset 0,5,0` to create a box safe zone that is 10m wide and 10m tall, centered 5m above the point where you are aiming.

### Edit a safe zone

To edit a safe zone created by this plugin, aim at the safe zone origin (where the debug text appears), and run the command `maedit safezone ...`. The arguments are the same as for `maspawn safezone ...`. See above for details.

### Move or rotate a safe zone

To move a safe zone created by this plugin, aim at the safe zone origin (where the debug text appears), and run the command `tls` from the [Telekinesis](https://umod.org/plugins/telekinesis) plugin. As you move your camera and use other features provided by Telekinesis, the safe zone will move automatically. When you are satisfied with the new position, run the command `tls` to save the new position.

Alternative ways to move a safe zone:
- Replace the safe zone by removing it (`makill`) and spawning it again (`maspawn safezone ...`), but that is more tedious.
- Directly edit the Monument Addons data file (for whichever profile you added the safe zone do), then reload the profile (`maprofile reload <name>`).

**Caution:** If you move a safe zone's origin more than 2m above a surface, it will no longer be possible to target the safe zone with commands such as `maedit`, `makill` or `tls`, so it's recommended to keep the safe zone origin within 2m of a surface. If you want to raise a safe zone above a surface, you can use the `offset` option which won't affect the ability to target the safe zone with commands.

**Tip:** If you have move a safe zone to an undesirable position or by accident, if it was the last thing you did with telekinesis and was done recently, you can revert the safe zone to the previous location with the command `tls undo`.

### Remove a safe zone

To remove a safe zone created by this plugin, aim at the safe zone origin (where the debug text appears), and run the command `makill`.

### Screenshots

![](https://raw.githubusercontent.com/WheteThunger/MonumentAddonsSafeZones/master/Images/FerryTerminalSafeZone.png)
