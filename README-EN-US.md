﻿# ChangeFolderIcon - Folder Icon Customization Tool  

[ChangeFolderIcon](https://github.com/YILING0013/ChangeFolderIcon) is a Windows folder icon customization tool developed using **WinUI3** and **Windows API**. It allows quick icon modification via drag-and-drop, supports batch icon updates for nested folders, and provides predefined icon styles for easy application.  

[中文版本](README.md)

---  

### Preview:  

![](./ChangeFolderIcon/Assets/Images/1_en-us.png)  

## Build from Source  

#### Development Requirements  

* **Visual Studio 2022** (Version 17.0 or later)  
* **.NET Desktop Development** workload  
* **Windows App SDK** development tools  

#### Build Steps  

- [ ] Install **Visual Studio 2022** with **.NET Desktop Development** and **WinUI App Development** workloads.  
- [ ] Clone the repository: `git clone https://github.com/YILING0013/ChangeFolderIcon.git` 
- [ ] Open `ChangeFolderIcon.sln` inside the `ChangeFolderIcon` folder.  
- [ ] In **Visual Studio**, right-click the solution → **Restore NuGet Packages**.  
- [ ] Press `Ctrl+Shift+B` or go to **Build** → **Build Solution**.  
- [ ] right-click `ElevatedWorker` project → **Publish**. Click the **Publish** button.  
- [ ] Select `ChangeFolderIcon` project, press `F5` to start debugging, or `Ctrl+F5` to start without debugging.

### Notes  
- Requires **Windows 10 or later**.  
- On first launch, go to **Settings** and wait for icon resources to load, then check for updates.
- if you encounter issues fetching icon resources, check your network connection or manually download the icon resource package and select it in the settings page.  
- To change the language, select a language in **Settings** and restart the app.  

## License  

This project is open-source under the [GPL-3.0 license](LICENSE).  

---  
### Acknowledgments

Special thanks to the contributors of [Folder-Ico](https://github.com/icon11-community/Folder-Ico) for providing some of the icon resources used in this project.  

---

⭐ If you find this project useful, please give it a **Star**! 🚀
