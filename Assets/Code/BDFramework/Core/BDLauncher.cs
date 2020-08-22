﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BDFramework.GameStart;
using SQLite4Unity3d;
using UnityEngine;
using BDFramework.ResourceMgr;
using BDFramework.Sql;
using BDFramework.UFlux;
using Game.ILRuntime;
using LitJson;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace BDFramework
{
    /// <summary>
    /// 框架的配置
    /// </summary>
    public class BDFrameConfig
    {
        /// <summary>
        /// 版本号
        /// </summary>
        public string Version { get; set; }
    }
    public class BDLauncher : MonoBehaviour
    {
        /// <summary>
        /// 框架的相关配置
        /// </summary>
        public BDFrameConfig FrameConfig { get; private set; }
        
        /// <summary>
        /// GameConfig
        /// </summary>
        [HideInInspector]
        public GameConfig GameConfig { get; private set; }

        public TextAsset ConfigContent;

        #region 对外的生命周期

        static public Action OnStart { get; set; }
        static public Action OnUpdate { get; set; }
        static public Action OnLateUpdate { get; set; }

        /// <summary>
        /// 当框架初始化完成
        /// </summary>
        static public Action OnBDFrameInitialized { get; set; }
        /// <summary>
        /// 当框架初始化完成
        /// </summary>
        static public Action OnBDFrameInitializedForTest { get; set; }
        #endregion
        
        static public BDLauncher Inst { get; private set; }


        // Use this for initialization
        private void Awake()
        {
            Inst = this;
            this.gameObject.AddComponent<IEnumeratorTool>();
            //框架配置
            LoadFrameConfig();
            //游戏配置
            if (Application.isEditor)
            {
                this.GameConfig = this.transform.GetComponent<Config>().Data;
            }
            else
            {
                if (this.ConfigContent)
                {
                    this.GameConfig = JsonMapper.ToObject<GameConfig>(this.ConfigContent.text);
                }
                else
                {
                    BDebug.LogError("GameConfig配置为null,请检查!");
                }
            }


            //启动本地
            LaunchLocal();
        }
        
        /// <summary>
        /// 加载框架配置
        /// </summary>
        private void LoadFrameConfig()
        {
            var content = Resources.Load<TextAsset>("BDFrameConfig").text;
            FrameConfig = JsonMapper.ToObject<BDFrameConfig>(content);
            //框架版本
            BDebug.Log("框架版本:" + FrameConfig.Version,"red");
        }
        
        #region 启动非热更逻辑

        private IGameStart mainStart;

        /// <summary>
        /// 启动本地代码
        /// </summary>
        public void LaunchLocal()
        {
            //寻找iGamestart
            foreach (var t in Assembly.GetExecutingAssembly().GetTypes())
            {
                if (t.IsClass && t.GetInterface("IGameStart") != null)
                {
                    var attr = (GameStartAtrribute) t.GetCustomAttribute(typeof(GameStartAtrribute), false);
                    if (attr != null && attr.Index == 0)
                    {
                        mainStart = Activator.CreateInstance(t) as IGameStart;
                        //注册
                        mainStart.Start();

                        break;
                    }
                }
            }

            //类型注册
            List<Type> types = new List<Type>();
            types.AddRange(typeof(Button).Assembly.GetTypes());
            types.AddRange(typeof(IButton).Assembly.GetTypes());
            var uitype = typeof(UIBehaviour);
            foreach (var t in types)
            {
                //注册所有uiComponent
                if (t.IsSubclassOf(uitype))
                {
                    ILRuntimeHelper.UIComponentTypes[t.FullName] = t;
                }
            }

            // if (Config.CodeRoot == AssetLoadPath.Editor)
            // {
            //     foreach (var t in types)
            //     {
            //         if (t.IsClass && t.GetInterface("IGameStart") != null)
            //         {
            //             var attr = (GameStartAtrribute) t.GetCustomAttribute(typeof(GameStartAtrribute), false);
            //             if (attr != null && attr.Index == 1)
            //             {
            //                 mainStart = Activator.CreateInstance(t) as IGameStart;
            //                 //注册
            //                 mainStart.Start();
            //
            //                 break;
            //             }
            //         }
            //     }
            // }
        }

        #endregion

        #region 启动热更逻辑

        /// <summary>
        /// 初始化
        /// 修改版本,让这个启动逻辑由使用者自行处理
        /// </summary>
        /// <param name="GameId">单游戏更新启动不需要id，多游戏更新需要id号</param>
        public void Launch(string GameId = "")
        {
            //初始化资源加载
            string coderoot = "";
            string sqlroot = "";
            string assetroot = "";

            //各自的路径
            //art
            if (GameConfig.ArtRoot == AssetLoadPath.Editor)
            {
                if (Application.isEditor)
                {
                    //默认不走AssetBundle
                    assetroot = "";
                }
                else
                {
                    //手机默认直接读取Assetbundle
                    assetroot = Application.persistentDataPath;
                }
            }
            else if (GameConfig.ArtRoot == AssetLoadPath.Persistent)
            {
                assetroot = Application.persistentDataPath;
            }

            else if (GameConfig.ArtRoot == AssetLoadPath.StreamingAsset)
            {
                if (string.IsNullOrEmpty(GameConfig.CustomArtRoot) == false)
                {
                    assetroot = GameConfig.CustomArtRoot;
                }
                else
                {
                    assetroot = Application.streamingAssetsPath;
                }
            }

            //sql
            if (GameConfig.SQLRoot == AssetLoadPath.Editor)
            {
                //sql 默认读streaming
                sqlroot = Application.streamingAssetsPath;
            }

            else if (GameConfig.SQLRoot == AssetLoadPath.Persistent)
            {
                sqlroot = Application.persistentDataPath;
            }
            else if (GameConfig.SQLRoot == AssetLoadPath.StreamingAsset)
            {
                sqlroot = Application.streamingAssetsPath;
            }

            //code
            if (GameConfig.CodeRoot == AssetLoadPath.Editor)
            {
                //sql 默认读streaming
                coderoot = "";
            }
            else if (GameConfig.CodeRoot == AssetLoadPath.Persistent)
            {
                coderoot = Application.persistentDataPath;
            }
            else if (GameConfig.CodeRoot == AssetLoadPath.StreamingAsset)
            {
                coderoot = Application.streamingAssetsPath;
            }

            //多游戏更新逻辑
            if (Application.isEditor == false)
            {
                if (GameId != "")
                {
                    assetroot = assetroot + "/" + GameId;
                    coderoot = coderoot + "/" + GameId;
                    sqlroot = sqlroot + "/" + GameId;
                }
            }

            //异步
            BResources.Load(assetroot, () =>
            {
                //sql
                SqliteLoder.LoadOnRuntime(sqlroot);
                //异步 这里如果代码很早的时候就开始走表格逻辑，有可能报错，
                //但是大部分游戏应该不会，三层回调太丑，暂时用这个
                ScriptLoder.Load(coderoot, GameConfig.CodeRunMode);
            });
        }

        #endregion


        //普通帧循环
        private void Update()
        {
            mainStart?.Update();
            OnUpdate?.Invoke();
        }

        //更快的帧循环
        private void LateUpdate()
        {
            mainStart?.LateUpdate();
            OnLateUpdate?.Invoke();
        }

        void OnApplicationQuit()
        {
#if UNITY_EDITOR
           
            SqliteLoder.Close();

            ILRuntimeHelper.Close();
#endif
        }
    }
}