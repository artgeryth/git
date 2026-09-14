// 关心提醒：①久坐 ②熬夜
//
// 判断依据只有一个：系统提供的「最后一次输入时间」（LayeredWindow.SystemIdleMs）。
// 不记录你打了什么、不读你的文件、不装钩子 —— 和程序一直以来的隐私承诺一致。
//
//   久坐：连续用电脑满 SitMinutes 分钟就提醒一次；
//         中途离开（无输入 ≥ RestMinutes 分钟）才算"休息过了"，重新计时。
//         提醒过就不再唠，直到你真的休息一次。
//   熬夜：凌晨 LateFromHour 点到 LateToHour 点之间，你还在用电脑就念一次，
//         一晚只念一次。
using System;

static class Care
{
    /// <summary>连续活动多久提醒久坐（分钟）</summary>
    public const int SitMinutes = 50;
    /// <summary>无输入超过这么久，算"休息过了"（分钟）</summary>
    public const int RestMinutes = 5;
    /// <summary>熬夜提醒的时段（凌晨几点到几点）</summary>
    public const int LateFromHour = 1;
    public const int LateToHour = 5;

    static DateTime sinceRest = DateTime.MinValue;   // 上次休息之后开始连续用电脑的时刻
    static bool sitReminded = false;
    static string lateRemindedDate = "";             // 一晚只念一次

    public static void Tick(App app)
    {
        if (app == null || app.pet == null) return;
        if (app.headless) return;
        if (!app.store.GetBool("careSit", true) && !app.store.GetBool("careLate", true)) return;

        int idleMs = LayeredWindow.SystemIdleMs();

        /* ---- 久坐 ---- */
        if (app.store.GetBool("careSit", true))
        {
            if (idleMs >= RestMinutes * 60000)
            {
                // 离开够久 —— 算休息过了，重新计时（也允许下次再提醒）
                sinceRest = DateTime.MinValue;
                sitReminded = false;
            }
            else
            {
                if (sinceRest == DateTime.MinValue) sinceRest = DateTime.Now;
                if (!sitReminded && (DateTime.Now - sinceRest).TotalMinutes >= SitMinutes)
                {
                    if (Quiet(app))
                    {
                        sitReminded = true;
                        app.Log("久坐提醒：连续活动已满 " + SitMinutes + " 分钟");
                        app.pet.SayCat("care_sit", "shy");
                    }
                }
            }
        }

        /* ---- 熬夜 ---- */
        if (app.store.GetBool("careLate", true) && !Quiet(app))
        {
            DateTime now = DateTime.Now;
            if (now.Hour >= LateFromHour && now.Hour < LateToHour)
            {
                string today = now.ToString("yyyy-MM-dd");
                if (lateRemindedDate != today)
                {
                    lateRemindedDate = today;
                    app.Log("熬夜提醒：" + now.ToString("HH:mm"));
                    app.pet.SayCat("care_late", "sleepy");
                }
            }
        }
    }

    /// <summary>别在她正忙 / 你在聊天的时候插嘴</summary>
    static bool Quiet(App app)
    {
        if (app.pet.Busy) return false;
        if (app.ChatVisible) return false;
        if (app.pet.MenuVisible) return false;
        return true;
    }

    /// <summary>自检用：把内部计时重置</summary>
    public static void ResetForTest()
    {
        sinceRest = DateTime.MinValue;
        sitReminded = false;
        lateRemindedDate = "";
    }

    /// <summary>自检用：直接问「连续活动 N 分钟后该不该提醒」</summary>
    public static string ProbeSit(int minutes)
    {
        if (minutes >= SitMinutes) return "care_sit";
        return null;
    }
}
