# Sep Download Manager v1

نسخه‌ی اول هسته‌ی Download Manager:
- رابط گرافیکی Windows Forms
- دانلود HTTP/HTTPS
- انتخاب 1 تا 16 بخش
- نمایش پیشرفت، حجم و سرعت
- Pause / Resume
- ذخیره در مسیر انتخاب‌شده

## Build
این پروژه برای `.NET Framework 4.8` ساخته شده و برای Windows 7 SP1 / 8.1 / 10 مناسب است.

در Visual Studio:
1. فایل `SepDownloadManager.sln` را باز کنید.
2. Configuration را روی `Release` بگذارید.
3. Build > Build Solution را بزنید.

نکته: Pause/Resume در این نسخه‌ی اولیه بعد از Resume دانلود را از ابتدا شروع می‌کند. در نسخه‌ی بعدی می‌توانیم Resume واقعی از محل قطع‌شده، صف دانلود، محدودیت سرعت و Tray را اضافه کنیم.
