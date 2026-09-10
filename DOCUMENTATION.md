# NezamMonitor Windows

## 📋 Table of Contents

- [Overview](#overview)
- [Features](#features)
- [System Requirements](#system-requirements)
- [Installation](#installation)
- [Quick Start](#quick-start)
- [User Guide](#user-guide)
- [Troubleshooting](#troubleshooting)
- [FAQ](#faq)
- [Support](#support)

---

## 📖 Overview

**NezamMonitor** (مانیتور نظام مهندسی) is a comprehensive case management system designed for engineering supervision offices. It helps manage cases, track engineers, calculate fees, generate reports, and maintain follow-up notes.

### Key Benefits

- ✅ **Centralized Management** - All case data in one place
- ✅ **Persian Support** - Full RTL layout and Persian text
- ✅ **Report Generation** - Automated Word document creation
- ✅ **Backup/Restore** - Secure data protection
- ✅ **Android Sync** - Transfer data to mobile devices
- ✅ **Offline First** - Works without internet connection

---

## 🎯 Features

### Case Management (مدیریت پرونده‌ها)
- Create and edit cases
- Track case status and history
- Store owner information
- Link specifications to cases

### Engineer Tracking (پیگیری مهندسین)
- Register engineers per case
- Track disciplines and roles
- Link engineers to projects
- Monitor engineer assignments

### Fee Calculation (محاسبه حق‌الزحمه)
- Calculate fees per case
- Track payment status
- Generate fee reports
- Support for multiple fee types

### Report Generation (تولید گزارش)
- Word document templates
- Automated report filling
- Multiple report types
- Print-ready output

### Follow-Up (پیگیری)
- Add notes to cases
- Track follow-up status
- Search and filter notes
- Export to Android

### Backup & Restore (پشتیبان‌گیری)
- Encrypted backup files
- Complete data restore
- Settings preservation
- Credential protection

### Android Export (خروجی اندروید)
- Transfer data to mobile
- Sync follow-up notes
- Bidirectional updates
- Change tracking

---

## 💻 System Requirements

### Minimum Requirements
- **OS:** Windows 10 (64-bit) or later
- **RAM:** 4 GB
- **Disk:** 100 MB free space
- **Display:** 1024x768

### Recommended
- **OS:** Windows 11 (64-bit)
- **RAM:** 8 GB
- **Disk:** 500 MB free space
- **Display:** 1920x1080

### Software Requirements
- **.NET 8.0 Desktop Runtime** - [Download](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Microsoft Word** (optional) - For viewing generated reports

---

## 🚀 Installation

### Step 1: Download
Download the latest release from:
- **GitHub Releases:** https://github.com/Barkhordari-dev/nezammonitor-windows/releases

Choose version:
- **Full Version** (~142MB) - Includes Playwright for auto-update
- **Lite Version** (~18MB) - Core features only

### Step 2: Extract
```
1. Right-click the ZIP file
2. Select "Extract All..."
3. Choose destination folder
4. Click "Extract"
```

### Step 3: Run
```
1. Open the extracted folder
2. Double-click "NezamMonitor.App.exe"
3. Wait for application to start
```

### Step 4: First Launch
- A `data/` folder will be created automatically
- The database will be initialized
- You can start adding cases immediately

---

## 🎯 Quick Start

### Adding Your First Case
1. Click **"پرونده‌ها"** (Cases) in the sidebar
2. Click **"➕ پرونده جدید"** (New Case)
3. Fill in the required fields:
   - شماره پرونده (Case Number)
   - نام مالک (Owner Name)
   - شماره تماس (Phone Number)
4. Click **"ذخیره"** (Save)

### Generating a Report
1. Select a case from the list
2. Click **"📄 گزارش‌ها"** (Reports)
3. Choose report type and stage
4. Click **"تولید گزارش"** (Generate Report)
5. Word document will be created in `outputs/` folder

### Creating a Backup
1. Go to **"⚙️ تنظیمات"** (Settings)
2. Click **"💾 ایجاد پشتیبان"** (Create Backup)
3. Choose save location
4. Enter backup password
5. Click **"ذخیره"** (Save)

---

## 📖 User Guide

### Dashboard (داشبورد)
- View case statistics
- Quick access to recent cases
- System status overview

### Cases (پرونده‌ها)
- **List View:** See all cases with filters
- **Detail View:** Edit case information
- **Search:** Find cases by number or owner
- **Filters:** Filter by status, date, etc.

### Engineers (مهندسین)
- **Add Engineer:** Link to case
- **Discipline:** Select engineering field
- **Role:** Assign as inspector, consultant, etc.
- **Contact:** Store phone and email

### Fees (حق‌الزحمه)
- **Calculate:** Auto-calculate based on rules
- **Track:** Monitor payment status
- **Report:** Generate fee statements
- **Export:** Send to Excel or Word

### Reports (گزارش‌ها)
- **Templates:** Pre-defined Word templates
- **Stages:** Phase 1, 2, 3 reports
- **Customize:** Modify template content
- **Print:** Direct print support

### Follow-Up (پیگیری)
- **Notes:** Add case notes
- **Status:** Track progress
- **Search:** Find specific notes
- **Export:** Send to Android app

### Settings (تنظیمات)
- **Theme:** Light/Dark mode
- **Language:** Persian/English
- **Backup:** Create/Restore backups
- **About:** Version information

---

## 🔧 Troubleshooting

### Application Won't Start

**Problem:** Double-clicking exe does nothing

**Solution:**
1. Install .NET 8.0 Desktop Runtime
2. Right-click exe → "Run as administrator"
3. Check antivirus software isn't blocking

### Database Errors

**Problem:** "Database is locked" or similar errors

**Solution:**
1. Close all instances of the application
2. Delete `data/nezam_monitor.db`
3. Restart the application

### Templates Not Found

**Problem:** Reports fail to generate

**Solution:**
1. Ensure `templates/` folder exists
2. Check .docx files are present
3. Verify Microsoft Word is installed

### Persian Text Display Issues

**Problem:** Text appears garbled

**Solution:**
1. Check Windows font settings
2. Install B Nazanin font
3. Restart the application

---

## ❓ FAQ

### Q: Where is my data stored?
**A:** All data is stored in `data/nezam_monitor.db` (SQLite database)

### Q: How do I backup my data?
**A:** Go to Settings → Backup, or manually copy the `data/` folder

### Q: Can I use this on multiple computers?
**A:** Yes, copy the entire folder to another computer

### Q: How do I update to a new version?
**A:** Download new version, copy `data/` folder to new location

### Q: Is my data secure?
**A:** Yes, data is stored locally and backups can be encrypted

### Q: Can I export to Excel?
**A:** Yes, use the Excel export feature in the toolbar

### Q: How do I generate reports?
**A:** Select a case → Reports → Choose template → Generate

### Q: Can I use the Android app?
**A:** Yes, use the Android export feature to transfer data

---

## 📞 Support

### Developer
**مهندس برخورداری**

### GitHub
- **Repository:** https://github.com/Barkhordari-dev/nezammonitor-windows
- **Issues:** https://github.com/Barkhordari-dev/nezammonitor-windows/issues

### Documentation
- **README:** See README.md in repository
- **Installation:** See INSTALL.md

---

## 📄 License

This software is proprietary. All rights reserved.

---

## 🔄 Version History

### v1.0.0 (Current)
- Initial release
- Case management
- Engineer tracking
- Fee calculation
- Report generation
- Follow-up notes
- Backup/Restore
- Android export

---

*Last updated: September 10, 2026*
