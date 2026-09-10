# NezamMonitor Windows - Installation Guide

## 📥 Download

Download the latest release from:
- **Full Version:** `NezamMonitor_v1.0.zip` (142MB)
- **Lite Version:** `NezamMonitor_v1.0_Lite.zip` (18MB)

## 🚀 Installation Steps

### Step 1: Extract Files
```
Right-click → Extract All → Select destination folder
```

### Step 2: Run Application
```
Double-click → NezamMonitor.App.exe
```

### Step 3: First Launch
- A `data/` folder will be created automatically
- Database will be initialized
- You can start using the application

## 📁 Folder Structure
```
NezamMonitor/
├── NezamMonitor.App.exe      ← Main executable
├── NezamMonitor.App.dll      ← App library
├── NezamMonitor.Core.dll     ← Core library
├── app_logo.png              ← App icon
├── templates/                ← Word templates
│   ├── مکانیک مرحله اول.docx
│   ├── مکانیک مرحله دوم.docx
│   └── مکانیک مرحله سوم.docx
├── data/                     ← Database (auto-created)
└── README.md                 ← This file
```

## ⚠️ Important Notes

### Backup Your Data
- The `data/` folder contains all your cases and settings
- **Always backup this folder regularly**
- Use the built-in Backup feature (Settings → Backup)

### Word Templates
- The `templates/` folder contains Word templates
- **Do not delete or modify** these files
- They are used for report generation

### Security
- Your data is stored locally in `data/nezam_monitor.db`
- **Never share** the `data/` folder with others
- **Never upload** the `data/` folder to cloud services

## 🔄 Updates

### Check for Updates
- The application will check for updates automatically
- You can also check manually in Settings → About

### Manual Update
1. Download the latest release
2. Extract to a new folder
3. Copy your `data/` folder from the old installation
4. Run the new `NezamMonitor.App.exe`

## 🐛 Troubleshooting

### Application Won't Start
- Make sure you have .NET 8.0 Runtime installed
- Download from: https://dotnet.microsoft.com/download/dotnet/8.0

### Database Errors
- Close the application
- Delete `data/nezam_monitor.db` (this will reset all data)
- Restart the application

### Templates Not Found
- Make sure the `templates/` folder exists
- Make sure the .docx files are in the correct location

## 📞 Support

For issues or questions:
- **Developer:** مهندس برخورداری
- **GitHub:** https://github.com/Barkhordari-dev/nezammonitor-windows

## 📋 System Requirements

- **OS:** Windows 10/11 (64-bit)
- **Runtime:** .NET 8.0 Desktop Runtime
- **RAM:** 4GB minimum
- **Disk:** 100MB free space

## 🎯 Features

- ✅ Case management (پرونده‌ها)
- ✅ Engineer tracking (مهندسین)
- ✅ Fee calculation (حق‌الزحمه)
- ✅ Report generation (گزارش‌ها)
- ✅ Follow-up notes (پیگیری)
- ✅ Backup/Restore
- ✅ Android export/import
- ✅ Word template support
- ✅ Persian/Arabic text support
- ✅ RTL layout
