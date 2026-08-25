using System;
using System.Collections;
using UnityEngine;
using VNovelizer.Core;

public class StaminaManager : BaseManager<StaminaManager>
{
    public static readonly bool IsEnabled = false;

    private const string CurrentStaminaKey = "vn_stamina_current";
    private const string MaxStaminaKey = "vn_stamina_max";
    private const string RecoveryStartedUtcTicksKey = "vn_stamina_recovery_started_utc_ticks";
    private const int DefaultMaxStamina = 100;
    private const int RecoveryAmount = 60;
    private const double RecoveryIntervalSeconds = 600d;

    private bool isInitialized;
    private DateTime recoveryStartedUtc;
    private Coroutine recoveryCoroutine;

    public event Action<int, int> OnStaminaChanged;
    public event Action OnRecoveryTimerChanged;

    public int CurrentStamina { get; private set; }
    public int MaxStamina { get; private set; }

    public void Init()
    {
        if (isInitialized)
            return;

        isInitialized = true;

        if (!IsEnabled)
        {
            MaxStamina = DefaultMaxStamina;
            CurrentStamina = MaxStamina;
            StopRecoveryCoroutine();
            NotifyChanged();
            OnRecoveryTimerChanged?.Invoke();
            return;
        }

        LoadOrCreateData();
        ApplyOfflineRecovery(DateTime.UtcNow);
        EnsureRecoveryTimerState();
        NotifyChanged();
    }

    public bool HasEnough(int amount = 1)
    {
        if (!IsEnabled)
            return true;

        Init();
        return amount <= 0 || CurrentStamina >= amount;
    }

    public bool TryConsume(int amount = 1)
    {
        if (!IsEnabled)
            return true;

        Init();

        if (amount <= 0)
            return true;

        if (CurrentStamina < amount)
            return false;

        SetCurrentStamina(CurrentStamina - amount);
        return true;
    }

    public void Restore(int amount)
    {
        if (!IsEnabled)
            return;

        Init();

        if (amount <= 0)
            return;

        SetCurrentStamina(Mathf.Min(CurrentStamina + amount, MaxStamina));
    }

    public void RestoreToFull()
    {
        if (!IsEnabled)
            return;

        Init();
        SetCurrentStamina(MaxStamina);
    }

    public void RestoreRecoveryAmount()
    {
        Restore(RecoveryAmount);
    }

    public bool IsRecoveryTimerVisible
    {
        get
        {
            if (!IsEnabled)
                return false;

            Init();
            return CurrentStamina < MaxStamina;
        }
    }

    public TimeSpan GetTimeUntilNextRecovery()
    {
        if (!IsEnabled)
            return TimeSpan.Zero;

        Init();

        if (CurrentStamina >= MaxStamina)
            return TimeSpan.Zero;

        EnsureRecoveryStartTime(DateTime.UtcNow);
        TimeSpan remaining = TimeSpan.FromSeconds(RecoveryIntervalSeconds) - (DateTime.UtcNow - recoveryStartedUtc);
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    public void SetMaxStamina(int maxStamina, bool refillIfCurrentExceedsMax = true)
    {
        if (!IsEnabled)
            return;

        Init();

        MaxStamina = Mathf.Max(1, maxStamina);
        GlobalDataManager.GetInstance().SetIntFlag(MaxStaminaKey, MaxStamina);

        if (refillIfCurrentExceedsMax && CurrentStamina > MaxStamina)
        {
            SetCurrentStamina(MaxStamina);
            return;
        }

        ApplyOfflineRecovery(DateTime.UtcNow);
        EnsureRecoveryTimerState();
        NotifyChanged();
    }

    private void LoadOrCreateData()
    {
        GlobalDataManager.GetInstance().Init();
        GlobalData data = GlobalDataManager.GetInstance().GetGlobalData();

        if (data.IntFlags == null)
            data.IntFlags = new System.Collections.Generic.Dictionary<string, int>();

        if (data.StringFlags == null)
            data.StringFlags = new System.Collections.Generic.Dictionary<string, string>();

        if (!data.IntFlags.ContainsKey(MaxStaminaKey) || data.IntFlags[MaxStaminaKey] <= 0)
            GlobalDataManager.GetInstance().SetIntFlag(MaxStaminaKey, DefaultMaxStamina);

        if (!data.IntFlags.ContainsKey(CurrentStaminaKey))
            GlobalDataManager.GetInstance().SetIntFlag(CurrentStaminaKey, DefaultMaxStamina);

        MaxStamina = Mathf.Max(1, GlobalDataManager.GetInstance().GetIntFlag(MaxStaminaKey));
        CurrentStamina = Mathf.Clamp(GlobalDataManager.GetInstance().GetIntFlag(CurrentStaminaKey), 0, MaxStamina);

        if (CurrentStamina != GlobalDataManager.GetInstance().GetIntFlag(CurrentStaminaKey))
            GlobalDataManager.GetInstance().SetIntFlag(CurrentStaminaKey, CurrentStamina);
    }

    private void SetCurrentStamina(int value)
    {
        CurrentStamina = Mathf.Clamp(value, 0, MaxStamina);
        GlobalDataManager.GetInstance().SetIntFlag(CurrentStaminaKey, CurrentStamina);
        EnsureRecoveryTimerState();
        NotifyChanged();
    }

    private bool ApplyOfflineRecovery(DateTime nowUtc)
    {
        if (CurrentStamina >= MaxStamina)
        {
            ClearRecoveryStartTime();
            return false;
        }

        EnsureRecoveryStartTime(nowUtc);

        TimeSpan elapsed = nowUtc - recoveryStartedUtc;
        if (elapsed < TimeSpan.Zero)
        {
            SaveRecoveryStartTime(nowUtc);
            return false;
        }

        long completedIntervals = (long)(elapsed.TotalSeconds / RecoveryIntervalSeconds);
        if (completedIntervals <= 0)
            return false;

        int restoreAmount = Mathf.Clamp((int)(completedIntervals * RecoveryAmount), 0, MaxStamina);
        CurrentStamina = Mathf.Min(CurrentStamina + restoreAmount, MaxStamina);
        GlobalDataManager.GetInstance().SetIntFlag(CurrentStaminaKey, CurrentStamina);

        if (CurrentStamina >= MaxStamina)
        {
            ClearRecoveryStartTime();
            return true;
        }

        DateTime nextStart = recoveryStartedUtc.AddSeconds(completedIntervals * RecoveryIntervalSeconds);
        SaveRecoveryStartTime(nextStart);
        return true;
    }

    private void EnsureRecoveryTimerState()
    {
        if (!isInitialized)
            return;

        if (CurrentStamina >= MaxStamina)
        {
            ClearRecoveryStartTime();
            StopRecoveryCoroutine();
            OnRecoveryTimerChanged?.Invoke();
            return;
        }

        EnsureRecoveryStartTime(DateTime.UtcNow);
        StartRecoveryCoroutine();
        OnRecoveryTimerChanged?.Invoke();
    }

    private void EnsureRecoveryStartTime(DateTime fallbackUtc)
    {
        if (TryLoadRecoveryStartTime(out DateTime startedUtc))
        {
            recoveryStartedUtc = startedUtc;
            return;
        }

        SaveRecoveryStartTime(fallbackUtc);
    }

    private bool TryLoadRecoveryStartTime(out DateTime startedUtc)
    {
        startedUtc = DateTime.MinValue;
        string ticksText = GlobalDataManager.GetInstance().GetStringFlag(RecoveryStartedUtcTicksKey);

        if (!long.TryParse(ticksText, out long ticks) || ticks <= 0)
            return false;

        try
        {
            startedUtc = new DateTime(ticks, DateTimeKind.Utc);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private void SaveRecoveryStartTime(DateTime startedUtc)
    {
        recoveryStartedUtc = DateTime.SpecifyKind(startedUtc, DateTimeKind.Utc);
        GlobalDataManager.GetInstance().SetStringFlag(RecoveryStartedUtcTicksKey, recoveryStartedUtc.Ticks.ToString());
    }

    private void ClearRecoveryStartTime()
    {
        recoveryStartedUtc = DateTime.MinValue;
        GlobalDataManager.GetInstance().SetStringFlag(RecoveryStartedUtcTicksKey, string.Empty);
    }

    private void StartRecoveryCoroutine()
    {
        if (recoveryCoroutine != null)
            return;

        MonoManager monoManager = MonoManager.GetInstance();
        if (monoManager != null)
            recoveryCoroutine = monoManager.StartCoroutine(RecoveryRoutine());
    }

    private void StopRecoveryCoroutine()
    {
        if (recoveryCoroutine == null)
            return;

        MonoManager.GetInstance().StopCoroutine(recoveryCoroutine);
        recoveryCoroutine = null;
    }

    private IEnumerator RecoveryRoutine()
    {
        WaitForSecondsRealtime wait = new WaitForSecondsRealtime(1f);

        while (CurrentStamina < MaxStamina)
        {
            if (ApplyOfflineRecovery(DateTime.UtcNow))
                NotifyChanged();

            if (CurrentStamina >= MaxStamina)
                break;

            OnRecoveryTimerChanged?.Invoke();
            yield return wait;
        }

        recoveryCoroutine = null;
        EnsureRecoveryTimerState();
    }

    private void NotifyChanged()
    {
        OnStaminaChanged?.Invoke(CurrentStamina, MaxStamina);
        EventCenter.GetInstance().EventTrigger(VNGameEvents.StaminaChanged);
    }
}
