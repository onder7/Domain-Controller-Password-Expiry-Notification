using CommunityToolkit.Mvvm.ComponentModel;

namespace PasswordExpiryNotification.Models;

public partial class EmailTemplateModel : ObservableObject
{
    public const string DefaultSubject =
        "[{CompanyName}] Şifreniz {DaysToExpire} gün içinde sona erecek";

    public const string DefaultHtmlBody =
        """
        <html>
        <body style="font-family: Segoe UI, Arial, sans-serif; color: #222; line-height: 1.5;">
          <p>Sayın <strong>{Name}</strong>,</p>
          <p>Kurumsal hesap şifrenizin (<strong>{UserName}</strong>) <strong>{DaysToExpire} gün</strong> içinde (<strong>{ExpiryDate}</strong> tarihinde) süresi dolacaktır.</p>
          <p>Erişiminizin kesilmemesi için lütfen şifrenizi süresi dolmadan yenileyiniz.</p>
          <p><strong>Nasıl değiştirebilirsiniz?</strong></p>
          <ul>
            <li>Kurumsal bilgisayarınızda Ctrl + Alt + Delete → <em>Şifreyi Değiştir</em></li>
            <li>veya şirket şifre değiştirme portalınız üzerinden</li>
          </ul>
          <p>Son şifre değişiminiz: <strong>{LastPasswordSet}</strong></p>
          <p>Bu mesaj otomatik gönderilmiştir. Yardım için IT Destek ile iletişime geçiniz.</p>
          <p style="color:#666; font-size:12px;">{CompanyName} — Bilgi Teknolojileri</p>
        </body>
        </html>
        """;

    [ObservableProperty]
    private string _subject = DefaultSubject;

    [ObservableProperty]
    private string _htmlBody = DefaultHtmlBody;

    public void ResetToDefaults()
    {
        Subject = DefaultSubject;
        HtmlBody = DefaultHtmlBody;
    }
}
