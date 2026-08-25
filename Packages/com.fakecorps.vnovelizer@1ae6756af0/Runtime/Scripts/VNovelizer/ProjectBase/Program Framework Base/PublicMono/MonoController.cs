using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
/// <summary>
/// 用于管理Mono
/// 1.生命周期函数
/// 2.事件
/// 3.协程
/// </summary>
public class MonoController : MonoBehaviour
{
    private event UnityAction updateEvent;
    private event UnityAction applicationQuitEvent;
    private event UnityAction<bool> applicationPauseEvent;

    void Start()
    {
        DontDestroyOnLoad(this.gameObject);
    }

    // Update is called once per frame
    void Update()
    {
        if (updateEvent != null)
        { 
            updateEvent();
        }
    }

    private void OnApplicationQuit()
    {
        applicationQuitEvent?.Invoke();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        applicationPauseEvent?.Invoke(pauseStatus);
    }

    //给外部提供的添加帧更新事件的函数
    public void AddUpdateListener(UnityAction fun)
    {
        updateEvent += fun;
        
    }
    //给外部提供的移除帧更新事件的函数 
    public void RemoveUpdateListener(UnityAction fun)
    {
        updateEvent -= fun;
    }

    public void AddApplicationQuitListener(UnityAction fun)
    {
        applicationQuitEvent += fun;
    }

    public void RemoveApplicationQuitListener(UnityAction fun)
    {
        applicationQuitEvent -= fun;
    }

    public void AddApplicationPauseListener(UnityAction<bool> fun)
    {
        applicationPauseEvent += fun;
    }

    public void RemoveApplicationPauseListener(UnityAction<bool> fun)
    {
        applicationPauseEvent -= fun;
    }
}
