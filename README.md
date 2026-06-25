# InventorySorter - 物品整理插件

## 功能介绍

这是一个用于未转变者 (Unturned) RocketMod 服务器的物品整理插件。

### 核心功能

1. **整理背包**：在背包界面点击"手上物品"即可整理背包物品
2. **整理箱子**：打开箱子时在背包界面执行同样操作会整理箱子内物品
3. **智能排序**：按物品种类和数量进行排序
4. **空间不足处理**：
   - 整理背包时：空间不足的物品掉落到地面
   - 整理箱子时：空间不足的物品先返还到背包，背包也没空间则掉落

### 触发方式

在背包界面将"手上物品"拖拽到"手上物品"位置（即点击拖拽自己），即可触发整理功能。

### 权限

无需任何权限，所有玩家均可使用。

## 安装步骤

1. 编译生成 `InventorySorter.dll`
2. 将 DLL 文件复制到服务器的 `Plugins` 文件夹
3. 重启服务器

## 编译步骤

### 准备工作

1. 安装 Visual Studio 2022 社区版
2. 新建「类库(.NET Framework 4.8)」项目
3. 引用以下 DLL 文件：
   - Assembly-CSharp.dll
   - Rocket.API.dll
   - Rocket.Core.dll
   - Rocket.Unturned.dll
   - UnityEngine.CoreModule.dll

### 编译

1. 将 Main.cs 的内容复制到项目中
2. 点击「生成解决方案」
3. 在 `bin/Release/` 文件夹中得到 `InventorySorter.dll`

## 文件结构

```
InventorySorter_Compile_Package/
├── InventorySorter.csproj   # Visual Studio 项目文件
├── Main.cs                   # 插件源代码
├── Dependencies/             # 依赖文件夹
└── README.md                 # 说明文档
```

## 注意事项

1. 插件不会导致任何物品消失或重叠
2. 整理时会先清空所有物品，再按顺序重新放置
3. 物品按 asset ID 排序，相同种类按数量排序
4. 无需配置文件，开箱即用

## 许可证

MIT License
