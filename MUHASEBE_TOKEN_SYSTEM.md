# Muhasebe Token Yönetim Sistemi

## 📋 Genel Bakış

Muhasebe uygulaması için özel JWT token yönetim sistemi. `sigortakullanicilist` tablosundan bağımsız, güvenli ve izlenebilir token yapısı.

## 🗄️ Database Tablosu

**Tablo Adı:** `muhasebe_kullanici_tokens`

**Özellikler:**
- JWT Access Token (1000 karakter)
- Refresh Token (255 karakter)
- Token expiry tarihleri
- Device tracking (User-Agent)
- IP Address tracking
- Token revocation (iptal etme)
- Session yönetimi

**Engine:** MyISAM
**Charset:** utf8mb4_unicode_ci

## 🔐 Güvenlik Özellikleri

### 1. Token Rotation
- Her refresh işleminde eski token iptal edilir, yeni token oluşturulur
- Token yeniden kullanım saldırılarını önler

### 2. Token Revocation
- Çıkış yapıldığında token database'de iptal edilir
- Sadece cookie silmekle kalmaz, aktif olarak iptal eder

### 3. Multi-Session Management
- Aynı kullanıcı birden fazla cihazdan giriş yapabilir
- Her session bağımsız olarak yönetilebilir
- Belirli bir cihazdan çıkış veya tüm cihazlardan çıkış

### 4. Device & IP Tracking
- Her token hangi cihaz ve IP'den oluşturulduğunu kaydeder
- Şüpheli giriş tespiti için kullanılabilir

### 5. Automatic Cleanup
- Süresi dolmuş token'lar otomatik temizlenebilir
- Admin endpoint ile manuel temizleme

## 📡 API Endpoints

### Authentication Endpoints

#### 1. Login
```http
POST /api/auth/login
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "password123"
}
```

**Response:**
```json
{
  "success": true,
  "message": "Giriş başarılı",
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "base64-encoded-token",
  "expiresIn": 7200,
  "user": {
    "id": 1,
    "name": "John Doe",
    "email": "user@example.com",
    "role": "admin",
    ...
  }
}
```

**Features:**
- IP ve Device bilgisi otomatik kaydedilir
- Eski aktif token'lar iptal edilir
- HttpOnly cookie ile refresh token gönderilir

---

#### 2. Refresh Token
```http
POST /api/auth/refresh
Content-Type: application/json

{
  "refreshToken": "base64-encoded-token"
}
```

**Response:**
```json
{
  "success": true,
  "token": "new-access-token",
  "refreshToken": "new-refresh-token",
  "expiresIn": 7200
}
```

**Features:**
- Token rotation (eski token iptal edilir)
- Yeni token'a device/IP bilgisi aktarılır
- Cookie'den veya body'den refresh token alınabilir

---

#### 3. Logout
```http
POST /api/auth/logout
Authorization: Bearer {access-token}
```

**Response:**
```json
{
  "success": true,
  "message": "Başarıyla çıkış yapıldı"
}
```

**Features:**
- Sadece mevcut session'ı iptal eder
- Token database'de revoke edilir
- Cookie temizlenir

---

#### 4. Logout All Devices
```http
POST /api/auth/logout-all
Authorization: Bearer {access-token}
```

**Response:**
```json
{
  "success": true,
  "message": "Başarıyla çıkış yapıldı"
}
```

**Features:**
- Kullanıcının TÜM aktif session'larını iptal eder
- Tüm cihazlardan çıkış yapmak için kullanılır

---

#### 5. Get Current User
```http
GET /api/auth/me
Authorization: Bearer {access-token}
```

**Response:**
```json
{
  "id": 1,
  "name": "John Doe",
  "email": "user@example.com",
  "role": "admin",
  ...
}
```

---

### Session Management Endpoints

#### 6. List Active Sessions
```http
GET /api/auth/sessions
Authorization: Bearer {access-token}
```

**Response:**
```json
{
  "success": true,
  "sessions": [
    {
      "id": 123,
      "deviceInfo": "Mozilla/5.0...",
      "ipAddress": "192.168.1.100",
      "createdAt": "2024-01-20T10:00:00",
      "lastUsedAt": "2024-01-20T15:30:00",
      "refreshTokenExpiry": "2024-01-27T10:00:00",
      "isCurrentSession": false
    }
  ]
}
```

**Features:**
- Kullanıcının tüm aktif session'larını listeler
- Device ve IP bilgisi gösterir
- Son kullanım zamanı takibi

---

#### 7. Revoke Specific Session
```http
DELETE /api/auth/sessions/{sessionId}
Authorization: Bearer {access-token}
```

**Response:**
```json
{
  "success": true,
  "message": "Oturum başarıyla sonlandırıldı"
}
```

**Features:**
- Belirli bir session'ı iptal eder
- Sadece kendi session'larını iptal edebilir
- Şüpheli girişleri uzaktan kapatma

---

### Admin Endpoints

#### 8. Cleanup Expired Tokens
```http
POST /api/auth/cleanup-tokens
Authorization: Bearer {admin-access-token}
```

**Response:**
```json
{
  "success": true,
  "deletedCount": 127,
  "message": "127 adet eski token temizlendi"
}
```

**Features:**
- Süresi dolmuş token'ları siler
- İptal edilmiş ve 30+ gün geçmiş token'ları temizler
- Database boyutunu optimize eder

---

## 🔄 Token Flow

### Login Flow
```
1. User → POST /api/auth/login
2. Backend → Validate credentials
3. Backend → Revoke old active tokens
4. Backend → Create new token record in DB
5. Backend → Set HttpOnly cookie
6. Backend → Return access token & user info
```

### Refresh Flow
```
1. User → POST /api/auth/refresh (with refresh token)
2. Backend → Validate refresh token from DB
3. Backend → Check user account status
4. Backend → Revoke old token (rotation)
5. Backend → Create new token record
6. Backend → Return new access token
```

### Logout Flow
```
1. User → POST /api/auth/logout
2. Backend → Get userId from JWT
3. Backend → Revoke token in DB
4. Backend → Delete HttpOnly cookie
5. Backend → Return success
```

---

## 📊 Database Schema

```sql
CREATE TABLE `muhasebe_kullanici_tokens` (
  `Id` INT(11) NOT NULL AUTO_INCREMENT,
  `KullaniciId` INT(11) NOT NULL,
  `AccessToken` VARCHAR(1000) NOT NULL,
  `AccessTokenExpiry` DATETIME NOT NULL,
  `RefreshToken` VARCHAR(255) NOT NULL,
  `RefreshTokenExpiry` DATETIME NOT NULL,
  `DeviceInfo` VARCHAR(500) NULL,
  `IpAddress` VARCHAR(50) NULL,
  `IsActive` TINYINT(1) NOT NULL DEFAULT 1,
  `IsRevoked` TINYINT(1) NOT NULL DEFAULT 0,
  `RevokeReason` VARCHAR(255) NULL,
  `CreatedAt` DATETIME NOT NULL,
  `LastUsedAt` DATETIME NULL,
  `RevokedAt` DATETIME NULL,

  PRIMARY KEY (`Id`),
  INDEX `idx_kullanici_id` (`KullaniciId`),
  INDEX `idx_refresh_token` (`RefreshToken`(250)),
  INDEX `idx_active_tokens` (`KullaniciId`, `IsActive`, `IsRevoked`),
  INDEX `idx_token_expiry` (`RefreshTokenExpiry`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb4;
```

---

## 🧪 Test Senaryoları

### 1. Normal Login Flow
```bash
# Login
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"test123"}'

# Use token
curl -X GET http://localhost:5000/api/auth/me \
  -H "Authorization: Bearer {access-token}"

# Refresh
curl -X POST http://localhost:5000/api/auth/refresh \
  -H "Content-Type: application/json" \
  -d '{"refreshToken":"{refresh-token}"}'

# Logout
curl -X POST http://localhost:5000/api/auth/logout \
  -H "Authorization: Bearer {access-token}"
```

### 2. Multi-Device Login
```bash
# Device 1 - Login
curl -X POST http://localhost:5000/api/auth/login \
  -H "User-Agent: Device1" \
  -d '{"email":"test@example.com","password":"test123"}'

# Device 2 - Login
curl -X POST http://localhost:5000/api/auth/login \
  -H "User-Agent: Device2" \
  -d '{"email":"test@example.com","password":"test123"}'

# List sessions
curl -X GET http://localhost:5000/api/auth/sessions \
  -H "Authorization: Bearer {access-token}"

# Logout from Device 1
curl -X POST http://localhost:5000/api/auth/logout \
  -H "Authorization: Bearer {device1-token}"

# Device 2 still works
curl -X GET http://localhost:5000/api/auth/me \
  -H "Authorization: Bearer {device2-token}"
```

### 3. Security - Token Revocation
```bash
# Login
TOKEN=$(curl -X POST http://localhost:5000/api/auth/login \
  -d '{"email":"test@example.com","password":"test123"}' \
  | jq -r '.token')

# Logout (revoke)
curl -X POST http://localhost:5000/api/auth/logout \
  -H "Authorization: Bearer $TOKEN"

# Try to use revoked token (should fail)
curl -X GET http://localhost:5000/api/auth/me \
  -H "Authorization: Bearer $TOKEN"
# Expected: 401 Unauthorized
```

---

## 📝 Implementation Files

### Domain Layer
- `IhsanAI.Domain/Entities/MuhasebeKullaniciToken.cs` - Entity definition

### Application Layer
**Commands:**
- `Features/Auth/Commands/LoginCommand.cs` - Login with device tracking
- `Features/Auth/Commands/RefreshTokenCommand.cs` - Token refresh with rotation
- `Features/Auth/Commands/LogoutCommand.cs` - Single/all device logout
- `Features/Auth/Commands/RevokeSessionCommand.cs` - Revoke specific session
- `Features/Auth/Commands/CleanupExpiredTokensCommand.cs` - Token cleanup

**Queries:**
- `Features/Auth/Queries/GetActiveSessionsQuery.cs` - List active sessions
- `Features/Auth/Queries/GetCurrentUserQuery.cs` - Get current user info

### Infrastructure Layer
- `Infrastructure/Persistence/ApplicationDbContext.cs` - DbContext with MuhasebeKullaniciTokens

### API Layer
- `Api/Endpoints/AuthEndpoints.cs` - All authentication endpoints

---

## 🚀 Deployment Checklist

- [x] Database tablosu oluşturuldu
- [x] Entity ve DbContext güncellendi
- [x] Login command güncellendi
- [x] Refresh command güncellendi
- [x] Logout command eklendi
- [x] Session management eklendi
- [x] Token cleanup eklendi
- [x] API endpoints hazır
- [ ] Frontend integration
- [ ] Test suite oluştur
- [ ] Background job ekle (otomatik cleanup)
- [ ] Admin panel için UI

---

## 🔮 Future Improvements

1. **Background Service**: Otomatik token temizleme (Hangfire/Quartz)
2. **Rate Limiting**: Login endpoint için brute-force koruması
3. **Email Notifications**: Yeni cihaz girişinde bildirim
4. **Two-Factor Auth**: 2FA desteği
5. **Token Analytics**: Token kullanım istatistikleri
6. **Geo-Location**: IP'den konum tespiti
7. **Device Fingerprinting**: Daha detaylı cihaz tanıma

---

## 📞 Support

Sorular için: [Proje repository]
