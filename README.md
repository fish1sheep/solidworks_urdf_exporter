# SolidWorks to URDF Exporter

Authored and maintained by [Stephen Brawner](brawner@gmail.com). Past supporters include [PickNik Consulting](https://picknik.ai), Verb Surgical, Open Robotics, and Willow Garage. 

## Changes from Upstream

This fork is based on [ros/solidworks_urdf_exporter](https://github.com/ros/solidworks_urdf_exporter) with the following improvements:

### ROS2 Migration

- **package.xml**: Upgraded to ROS2 format 3 with `ament_cmake` build tool, added `<build_depend>` support, configurable author and version
- **CMakeLists.txt**: Switched to `ament_cmake`, added `if(BUILD_TESTING)` and `ament_lint_auto` support
- **display.launch.py**: Rewritten as a full ROS2 Python launch file with `urdf_path`, `use_sim_time`, `use_gui`, and `rviz_config` launch arguments
- **joint_names YAML**: Uses ROS2-neutral format (`joint_names` instead of ROS1 `controller_joint_names`)
- **Mesh paths**: Unified to `package://` URIs, resolved via ament resource index

### Bug Fixes

- Fixed `display.launch.py` where the `urdf_path` launch argument was declared but never used
- Fixed part export mode writing legacy `manifest.xml` instead of `package.xml`

### Packaging

- Fixed Inno Setup installer script (`INSTALL/Install.iss`) for Release configuration
- Added ROS2 colcon build artifacts (`build/`, `install/`) to `.gitignore`

### Known Limitations

- Gazebo simulation launch file not yet implemented
- No ros2_control or xacro support (planned)

---

## Installation & Usage

### Option 1: Pre-built Installer

1. Download `sw2urdfSetup.exe` from [Releases](../../releases)
2. Run as **Administrator** and follow the wizard
3. Launch SolidWorks — "Export as URDF" appears under the **Tools** menu

### Option 2: Build the Installer

1. Install [Inno Setup](https://jrsoftware.org/isdl.php) (free)
2. Build `SW2URDF` in Visual Studio with **Release + x64** configuration
3. Open `INSTALL\Install.iss` in Inno Setup, **Build → Compile**
4. The installer is generated at `INSTALL\OUTPUT\sw2urdfSetup.exe`

### Option 3: Development / Debug

1. Build in Visual Studio with **Debug + x64** configuration
2. The post-build event auto-registers the COM DLL via `RegAsm.exe /codebase`
3. Launch SolidWorks directly — the add-in loads automatically

### Using Exported Packages with ROS2

Copy the exported package to your ROS2 workspace, build, and launch:

```bash
cp -r <PackageName> ~/ros2_ws/src/
cd ~/ros2_ws
colcon build --packages-select <PackageName>
source install/setup.bash
ros2 launch <PackageName> display.launch.py
```

Optional launch arguments:

```bash
ros2 launch <PackageName> display.launch.py \
    urdf_path:=/path/to/custom.urdf \
    use_sim_time:=true \
    use_gui:=true
```

### Uninstall

Via Windows Control Panel → Programs and Features → Uninstall "SolidWorks To URDF", or re-run the installer and choose Remove.

---

## SolidWorks Version Requirements

1. The minimum required version of SolidWorks for use with this add-in is 2018 Service Pack 5. SolidWorks 2017 or earlier may work. See [this issue](https://github.com/ros/solidworks_urdf_exporter/issues/73).

## Usage

See the [ROS Wiki](http://wiki.ros.org/sw_urdf_exporter) and associated [tutorials](http://wiki.ros.org/sw_urdf_exporter/Tutorials).

## Development

1. Install Visual Studio 2017
1. Install .NET desktop development
    1. From Visual Studio: `Tools > Get Tools and Features...`
    1. Check `.NET desktop development` package
    1. Select `Modify`
1. Install the [SolidWorks API tools](https://help.solidworks.com/2019/english/api/sldworksapiprogguide/GettingStarted/SolidWorks_API_Getting_Started_Overview.htm)
1. Launch Visual Studio with admin privileges. Right click and select `Run as Administrator`
1. Open `sw2urdf/SW2URDF.sln`  
1. Enable Debugging
    1. Right click `SW2URDF` in the Solution Explorer
    1. Click the `Debug` Tab
    1. Ensure `Configuration:` is set to `Debug`
    1. Ensure `Start external program:` is pointing to the SolidWorks executable. For example `C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS\SLDWORKS.exe`

## Converting mesh format from 3dxml to dae

Executing the following command will convert the format of the exported mesh from 3DXML to DAE, and rewrite the URDF, allowing you to display colored meshes in visualization tools like RViz:

```bash
pip3 install scikit-robot -U
convert-urdf-mesh <URDF_PATH> --output <OUTPUT_URDF_PATH>
```

### Trouble Shooting

1. `AxImp.exe` error - Check the installation of the .Net Tools. If there is no error, install the Windows 10 SDK.
1. `Resourse.resx` error - Check if `sw2urdf/SW2URDF/Resources.resx` exists and is empty. If empty, delete this file then right click the `SW2URDF` in the Solution Explorer and select `Properties`. Navigate to the Resources tab and click the button to create a new file.
