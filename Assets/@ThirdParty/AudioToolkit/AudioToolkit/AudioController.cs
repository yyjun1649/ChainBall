/*************************************************************
 *       Unity Audio Toolkit (c) by ClockStone 2013          *
 * 
 * Provides useful features for playing audio files in Unity:
 * 
 *  - ease of use: play audio files with a simple static function call, creation 
 *    of required AudioSource objects is handled automatically 
 *  - conveniently define audio assets in categories
 *  - play audios from within the inspector
 *  - set properties such as the volume for the entire category
 *  - change the volume of all playing audio objects within a category at any time
 *  - define alternative audio clips that get played with a specified 
 *    probability or order
 *  - advanced audio pick modes such as "RandomNotSameTwice", "TwoSimultaneously", etc.
 *  - uses audio object pools for optimized performance particularly on mobile devices
 *  - set audio playing parameters conveniently, such as: 
 *      + random pitch & volume
 *      + minimum time difference between play calls
 *      + delay
 *      + looping
 *  - fade out / in 
 *  - special functions for music including cross-fading 
 *  - music track playlist management with shuffle, loop, etc.
 *  - delegate event call if audio was completely played
 *  - audio event log
 *  - audio overview window
 * 
 * 
 * Usage:
 *  - create a unique GameObject named "AudioController" with the 
 *    AudioController script component added
 *  - Create an AudioObject prefab containing the following components: Unity's AudioSource, the AudioObject script, 
 *    and the PoolableObject script (if pooling is wanted). 
 *    Then set your custom AudioSource parameters in this prefab. Next, specify your custom prefab in the AudioController as 
 *    the "audio object".
 *  - create your audio categories in the AudioController using the Inspector, e.g. "Music", "SFX", etc.
 *  - for each audio to be played by a script create an 'audio item' with a unique name. 
 *  - specify any number of audio sub-items (= the AudioClip plus parameters) within an audio item. 
 *  - to play an audio item call the static function 
 *    AudioController.Play( "MyUniqueAudioItemName" )
 *  - Use AudioController.PlayMusic( "MusicAudioItemName" ) to play music. This function assures that only 
 *    one music file is played at a time and handles cross fading automatically according to the configuration
 *    in the AudioController instance
 *  - Note that when you are using pooling and attach an audio object to a parent object then make sure the parent 
 *    object gets deleted using ObjectPoolController.Destroy()
 *   
 ************************************************************/

#if UNITY_3_0 || UNITY_3_1 || UNITY_3_2 || UNITY_3_3 || UNITY_3_4 || UNITY_3_5
#define UNITY_3_x
#endif

#if UNITY_FLASH && !UNITY_3_x
#error Due to Unity not supporting Flash anymore we can not support Audio Toolkit export to Flash for Unity v4 or newer (only 3.x)
#endif

#if UNITY_3_x || UNITY_4_0 || UNITY_4_0_1 || UNITY_FLASH
#define UNITY_AUDIO_FEATURES_4_0
#else
#define UNITY_AUDIO_FEATURES_4_1
#endif

#if !UNITY_3_x && !UNITY_4_0 && !UNITY_4_1 && !UNITY_4_2 && !UNITY_4_3 && !UNITY_4_4 && !UNITY_4_5 && !UNITY_4_6 && !UNITY_4_7
#define UNITY_5_OR_NEWER
#endif

using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;
using CS.Essentials;
using CS.Essentials.Math; 
using UnityEngine.Serialization;

#pragma warning disable 1591 // undocumented XML code warning

namespace CS.AudioToolkit
{

    /// <summary>
    /// The audio managing class used to define and play audio items and categories.
    /// </summary>
    /// <remarks>
    /// At least one instance of an AudioController must exist in each scene using the Audio Toolkit. Usually there is 
    /// exactly one controller, but you can have additional controllers if they are marked as such (in the Unity insepector, see <see cref="isAdditionalAudioController"/>)
    /// There a two options when setting up an AudioController. Either you can specify all audio files that are used in your
    /// entire project in one single AudioController. Then add this AudioController to your initial scene and set 
    /// it persistent from within the inspector, so it will survive when a new scene is loaded. This way all audios
    /// are accessible from within your entire application. If you have a lot of audio files though, this may lead 
    /// to a lengthy loading time and will have a rather large memory footprint. To avoid this, you can alternatively 
    /// set up a specific AudioController for each scene which only contains those audio files needed in the particular 
    /// scene.
    /// </remarks>
    /// <example>
    /// Once you have defined your audio categories and items in the Unity inspector you can play music and sound effects 
    /// very easily:
    /// <code>
    /// AudioController.Play( "MySoundEffect1" );
    /// AudioController.Play( "MySoundEffect2", new Vector3( posX, posY, posZ ) );
    /// AudioController.PlayMusic( "MusicTrack1" );
    /// AudioController.SetCategoryVolume( "Music", 0.5f );
    /// AudioController.PauseMusic();
    /// </code>
    /// </example>
    /// 

#if AUDIO_TOOLKIT_DEMO
[AddComponentMenu( "ClockStone/Audio/AudioController Demo" )]
public class AudioController : MonoBehaviour, ISingletonMonoBehaviour // can not make DLL with SingletonMonoBehaviour
{
    static public AudioController Instance 
    {
        get {
            return UnitySingleton<AudioController>.GetSingleton( true, false );
        }
    }
    static public bool DoesInstanceExist()
    {
        var instance = UnitySingleton<AudioController>.GetSingleton( false, false );
        return !UnityEngine.Object.Equals( instance, null );
    }
#else
    [AddComponentMenu( "ClockStone/Audio/AudioController" )]
    public class AudioController : SingletonMonoBehaviour<AudioController>, ISerializationCallbackReceiver
    {
#endif

        /// <summary>
        /// A string containing the version number of the Audio Toolkit
        /// </summary>
        public const string AUDIO_TOOLKIT_VERSION = "11.1";

        /// <summary>
        /// Disables all audio playback.
        /// </summary>
        /// <remarks>
        /// Does not stop currently playing audios. Call <see cref="StopAll()"/> to stop all currently playing.
        /// </remarks>
        public bool DisableAudio
        {
            set
            {
                if( value != _audioDisabled )
                {
                    if( value == true )
                    {
                        // changed in v3.6 - allows to disable Audio without stopping all current audios

                        /*if ( AudioController.DoesInstanceExist() ) // value can be changed by inspector in none-playmode.
                        {
                            StopAll();
                        }*/
                    }
                    _audioDisabled = value;
                }
            }
            get
            {
                return _audioDisabled;
            }
        }

        /// <summary>
        /// You may use several AudioControllers in the same scene in parallel. All but one (the main controller) must be marked as 'additional'. 
        /// You can play audio items of any of those controllers with the normal Play() calls.
        /// </summary>
        /// <remarks>
        /// This can be used for games with a large amount of audio where you don't want all audio to be in memory at all time. 
        /// In this case use a persistent main AudioController for audios shared between all scenes of your game, and additional AudioControllers 
        /// for each scene containing specific audio for this level.
        /// </remarks>
        public bool isAdditionalAudioController
        {
            get
            {
                return _isAdditionalAudioController;
            }
            set // to be changed only from within the inspector
            {
                _isAdditionalAudioController = value;
            }
        }

        /// <summary>
        /// The global volume applied to all categories.
        /// You change the volume by script and the change will be apply to all 
        /// playing audios immediately.
        /// </summary>
        public float Volume
        {
            get { return _volume; }
            set { if( value != _volume ) { _volume = value; _ApplyVolumeChange(); } }
        }

        /// <summary>
        /// You must specify your AudioObject prefab here using the Unity inspector.
        /// <list type="bullet">
        ///     <listheader>
        ///          <description>The prefab must have the following components:</description>
        ///     </listheader>
        ///     <item>
        ///       <term>AudioObject</term>
        ///       <term>AudioSource (Unity built-in)</term>
        ///       <term>PoolableObject</term> <description>only required if pooling is uses</description>
        ///     </item>
        /// </list>
        ///  
        /// </summary>
        public GameObject AudioObjectPrefab;

        /// <summary>
        /// If enabled, the audio controller will survive scene changes
        /// </summary>
        /// <remarks>
        /// For projects with a large number of audio files you may consider having 
        /// separate AudioController version for each scene and only specify those audio items 
        /// that are really required in this scene. This can reduce memory consumption and
        /// speed up loading time for the initial scene.
        /// </remarks>
        public bool Persistent = false;

        /// <summary>
        /// If enabled all audio resources (AudioClips) specified in this AudioController are unloaded 
        /// from memory when the AudioController gets destroyed (e.g. when loading a new scene and <see cref="Persistent"/> 
        /// is not enabled)
        /// </summary>
        /// <remarks>
        /// Uses Unity's <c>Resources.UnloadAsset(...) </c>method. Can be used to save memory if many audio ressources are in use.
        /// It is recommended to use additional AudioControllers for audios that are used only within a specific scene, and a primary
        /// persistent AudioController for audio used throughout the entire application.
        /// </remarks>
        public bool UnloadAudioClipsOnDestroy = false;

        /// <summary>
        /// Enables / Disables AudioObject pooling
        /// </summary>
        /// <remarks>
        /// Warning: Use <see cref="PoolableReference{T}"/> to store an AudioObject reference if you have pooling enabled.
        /// </remarks>
        public bool UsePooledAudioObjects = true;

        /// <summary>
        /// If disabled, audios are not played if they have a resulting volume of zero.
        /// </summary>
        public bool PlayWithZeroVolume = false;

        /// <summary>
        /// If enabled fading is adjusted in a way so that cross-fades should result in the same power during the time of fadeing
        /// </summary>
        /// <remarks>
        /// Unfortunately not 100% correct as Unity uses unknown internal formulas for computing the volume.
        /// </remarks>
        public bool EqualPowerCrossfade = false;

        [SerializeField]
        AudioChannelWithInternalInfos[] audioChannels;

        /// <summary>
        /// returns the AudioChannel for the given AudioChannelType
        /// </summary>
        /// <param name="channel"></param>
        public static AudioChannel GetAudioChannel( AudioChannelType channel )
        {
            int idx = (int)channel;
            if( idx <0 || idx >= Instance.audioChannels.Length )
            {
                Debug.LogError( "Invalid AudioChannelType: " + channel );
                return null;
            }
            return Instance.audioChannels[idx];
        }

        internal static AudioChannelWithInternalInfos _GetAudioChannelInternal( AudioChannelType channel )
        {
            int idx = (int)channel;
            if( idx < 0 || idx >= Instance.audioChannels.Length )
            {
                Debug.LogError( "Invalid AudioChannelType: " + channel );
                return null;
            }
            return Instance.audioChannels[idx];
        }

        /// <summary>
        /// Returns the AudioChannel with the given index
        /// </summary>
        /// <param name="idx">Index from 0 to 7</param>
        /// <returns></returns>
        public AudioChannel GetAudioChannelWithIndex( int idx )
        {
            if( idx < 0 || idx >= audioChannels.Length )
            {
                Debug.LogError( "Invalid AudioChannelType: " + idx );
                return null;
            }
            return audioChannels[idx];
        }

        /// <summary>
        /// Gets or sets the soundMuted.
        /// </summary>
        /// <value>
        ///   <c>true</c> enables sound mute; <c>false</c> disables sound mute
        /// </value>
        /// <remarks>
        /// 'Sound' means all audio except music nd ambience sound
        /// </remarks>
        public bool soundMuted
        {
            get { return _soundMuted; }
            set
            {
                _soundMuted = value;
                _ApplyVolumeChange();
            }
        }

        #region field for upgrading from v10 or older

        // these field are obsolete since v11, use AudioController.GetChannel(AudioChannelType) to access these properties specific to each channel
        [SerializeField]
        private float musicCrossFadeTime = 0;
       
        [SerializeField]
        private float ambienceSoundCrossFadeTime = 0;

        [SerializeField]
        private bool specifyCrossFadeInAndOutSeperately = false;

        [SerializeField]
        private float _musicCrossFadeTime_In = 0;

        [SerializeField]
        private float _musicCrossFadeTime_Out = 0;

        [SerializeField]
        private float _ambienceSoundCrossFadeTime_In = 0;

        [SerializeField]
        private float _ambienceSoundCrossFadeTime_Out = 0;

        [SerializeField]
        private bool loopPlaylist = false;

        [SerializeField]
        private bool shufflePlaylist = false;

        [SerializeField]
        private bool crossfadePlaylist = false;

        [SerializeField]
        private float delayBetweenPlaylistTracks = 1;

        #endregion

        /// <summary>
        /// Specify your audio categories here using the Unity inspector.
        /// </summary>
        public AudioCategory[] AudioCategories;

        /// <summary>
        /// allows to specify a list of named playlist that can be played as music
        /// </summary>
        [FormerlySerializedAs( "musicPlaylists" )]
        public Playlist[] playlists = new Playlist[1];

        /// <summary>
        /// Event that is trigered whenever a playlist finished playing
        /// </summary>
        public Action<AudioChannelType,Playlist> playlistFinishedEvent;

        /// <summary>
        /// Returns the high precision audio system time size the application launch.
        /// </summary>
        /// <remarks>
        /// The audio system time does not increase if the application is paused.
        /// For performance reasons the time only gets updated with the frame rate. However,
        /// the time value does not lose precision even if the application is running for a
        /// long time (unlike Unity's 32bit float Time.systemTime
        /// </remarks>
        static public double systemTime
        {
            get
            {
                return _systemTime;
            }
        }

        /// <summary>
        /// Returns the high precision audio system delta time since the last frame update.
        /// </summary>
        static public double systemDeltaTime
        {
            get
            {
                return _systemDeltaTime;
            }
        }

        // **************************************************************************************************/
        //          public functions
        // **************************************************************************************************/

        /// <summary>
        /// Plays an audio item with the name <c>audioID</c> as music.
        /// </summary>
        /// <param name="audioID">The audio ID.</param>
        /// <param name="volume">The volume betweeen 0 and 1 [default=1].</param>
        /// <param name="delay">The delay [default=0].</param>
        /// <param name="startTime">The start time [default=0]</param>
        /// <returns>
        /// Returns the reference of the AudioObject that is used to play the audio item, or <c>null</c> if the audioID does not exist. 
        /// Warning: Use <see cref="PoolableReference{T}"/> to store an AudioObject reference if you have pooling enabled.
        /// </returns>
        /// <remarks>
        /// PlayMusic makes sure that only one music track is played at a time. If music cross fading is enabled in the AudioController
        /// fading is performed automatically.<br/>
        /// The audio clip the object will be placed right 
        /// in front of the current audio listener which is usually on the main camera. Note that the audio object will not
        /// be parented - so you will hear when the audio listener moves.
        /// </remarks>
        static public AudioObject PlayMusic( string audioID, float volume = 1, float delay = 0, float startTime = 0 )
        {
            return Instance._PlayInSingleTrackChannel( audioID, AudioChannelType.Music, volume, delay, startTime );
        }

        /// <summary>
        /// Plays an audio item with the name <c>audioID</c> as music at the specified position.
        /// </summary>
        /// <param name="audioID">The audio ID.</param>
        /// <param name="worldPosition">The position in world coordinates.</param>
        /// <param name="parentObj">The parent transform or <c>null</c>.</param>
        /// <param name="volume">The volume between 0 and 1 [default=1].</param>
        /// <param name="delay">The delay [default=0].</param>
        /// <param name="startTime">The start time [default=0]</param>
        /// <returns>
        /// Returns the reference of the AudioObject that is used to play the audio item, or <c>null</c> if the audioID does not exist. 
        /// Warning: Use <see cref="PoolableReference{T}"/> to store an AudioObject reference if you have pooling enabled.
        /// </returns>
        /// <remarks>
        /// PlayMusic makes sure that only one music track is played at a time. If music cross fading is enabled in the AudioController
        /// fading is performed automatically.
        /// </remarks>
        static public AudioObject PlayMusic( string audioID, Vector3 worldPosition, Transform parentObj = null, float volume = 1, float delay = 0, float startTime = 0 )
        {
            return Instance._PlayInSingleTrackChannel( audioID, AudioChannelType.Music, worldPosition, parentObj, volume, delay, startTime );
        }

        /// <summary>
        /// Plays an audio item with the name <c>audioID</c> as music at the specified position.
        /// </summary>
        /// <param name="audioID">The audio ID.</param>
        /// <param name="parentObj">The parent transform or <c>null</c>.</param>
        /// <param name="volume">The volume between 0 and 1 [default=1].</param>
        /// <param name="delay">The delay [default=0].</param>
        /// <param name="startTime">The start time [default=0]</param>
        /// <returns>
        /// Returns the reference of the AudioObject that is used to play the audio item, or <c>null</c> if the audioID does not exist. 
        /// Warning: Use <see cref="PoolableReference{T}"/> to store an AudioObject reference if you have pooling enabled.
        /// </returns>
        /// <remarks>
        /// PlayMusic makes sure that only one music track is played at a time. If music cross fading is enabled in the AudioController
        /// fading is performed automatically.
        /// </remarks>
        static public AudioObject PlayMusic( string audioID, Transform parentObj, float volume = 1, float delay = 0, float startTime = 0 )
        {
            return Instance._PlayInSingleTrackChannel ( audioID, AudioChannelType.Music, parentObj.position, parentObj, volume, delay, startTime );
        }

        /// <summary>
        /// Plays an audio item with the name <c>audioID</c> as ambience sound.
        /// </summary>
        /// <param name="audioID">The audio ID.</param>
        /// <param name="volume">The volume betweeen 0 and 1 [default=1].</param>
        /// <param name="delay">The delay [default=0].</param>
        /// <param name="startTime">The start time [default=0]</param>
        /// <returns>
        /// Returns the reference of the AudioObject that is used to play the audio item, or <c>null</c> if the audioID does not exist. 
        /// Warning: Use <see cref="PoolableReference{T}"/> to store an AudioObject reference if you have pooling enabled.
        /// </returns>
        /// <remarks>
        /// PlayAmbienceSound makes sure that only one ambience track is played at a time. If cross fading is enabled in the AudioController
        /// fading is performed automatically.<br/>
        /// The audio clip the object will be placed right 
        /// in front of the current audio listener which is usually on the main camera. Note that the audio object will not
        /// be parented - so you will hear when the audio listener moves.
        /// </remarks>
        static public AudioObject PlayAmbienceSound( string audioID, float volume = 1, float delay = 0, float startTime = 0 )
        {
            return Instance._PlayInSingleTrackChannel( audioID, AudioChannelType.Ambience, volume, delay, startTime );
        }

        /// <summary>
        /// Plays an audio item with the name <c>audioID</c> as ambience sound at the specified position.
        /// </summary>
        /// <param name="audioID">The audio ID.</param>
        /// <param name="worldPosition">The position in world coordinates.</param>
        /// <param name="parentObj">The parent transform or <c>null</c>.</param>
        /// <param name="volume">The volume between 0 and 1 [default=1].</param>
        /// <param name="delay">The delay [default=0].</param>
        /// <param name="startTime">The start time [default=0]</param>
        /// <returns>
        /// Returns the reference of the AudioObject that is used to play the audio item, or <c>null</c> if the audioID does not exist. 
        /// Warning: Use <see cref="PoolableReference{T}"/> to store an AudioObject reference if you have pooling enabled.
        /// </returns>
        /// <remarks>
        /// PlayAmbienceSound makes sure that only one ambience track is played at a time. If cross fading is enabled in the AudioController
        /// fading is performed automatically.
        /// </remarks>
        static public AudioObject PlayAmbienceSound( string audioID, Vector3 worldPosition, Transform parentObj = null, float volume = 1, float delay = 0, float startTime = 0 )
        {
            return Instance._PlayInSingleTrackChannel( audioID, AudioChannelType.Ambience, worldPosition, parentObj, volume, delay, startTime );
        }

        /// <summary>
        /// Plays an audio item with the name <c>audioID</c> as ambience sound at the specified position.
        /// </summary>
        /// <param name="audioID">The audio ID.</param>
        /// <param name="parentObj">The parent transform or <c>null</c>.</param>
        /// <param name="volume">The volume between 0 and 1 [default=1].</param>
        /// <param name="delay">The delay [default=0].</param>
        /// <param name="startTime">The start time [default=0]</param>
        /// <returns>
        /// Returns the reference of the AudioObject that is used to play the audio item, or <c>null</c> if the audioID does not exist. 
        /// Warning: Use <see cref="PoolableReference{T}"/> to store an AudioObject reference if you have pooling enabled.
        /// </returns>
        /// <remarks>
        /// PlayAmbienceSound makes sure that only one ambience track is played at a time. If cross fading is enabled in the AudioController
        /// fading is performed automatically.
        /// </remarks>
        static public AudioObject PlayAmbienceSound( string audioID, Transform parentObj, float volume = 1, float delay = 0, float startTime = 0 )
        {
            return Instance._PlayInSingleTrackChannel( audioID, AudioChannelType.Ambience, parentObj.position, parentObj, volume, delay, startTime );
        }

        /// <summary>
        /// Enqueues an audio ID to the music playlist queue.
        /// </summary>
        /// <param name="audioID">The audio ID.</param>
        /// <returns>
        /// The number of music tracks on the playlist.
        /// </returns>
        static public int EnqueueMusic( string audioID )
        {
            return Instance._EnqueueOnChannel( audioID, AudioChannelType.Music );
        }

        /// <summary>
        /// Enqueues an audio ID to the music playlist queue.
        /// </summary>
        /// <param name="audioID">The audio ID.</param>
        /// <returns>
        /// The number of music tracks on the playlist.
        /// </returns>
        static public int EnqueueOnChannel( string audioID, AudioChannelType channel )
        {
            return Instance._EnqueueOnChannel( audioID, channel);
        }

        private Playlist _GetCurrentPlaylist( AudioChannelType channel )
        {
            var channelData = _GetAudioChannelInternal( channel );
            return channelData.currentPlaylist;
        }

        /// <summary>
        /// Retrieves a playlist by name. If playlists are named identically it will return the first one it finds
        /// </summary>
        /// <param name="playlistName">The playlist's name</param>
        /// <returns>A playlist with the specified name, otherwise null</returns>
        public Playlist GetPlaylistByName( string playlistName )
        {
            for( int i = 0; i < playlists.Length; ++i )
            {
                if( playlistName == playlists[i].name )
                    return playlists[i];
            }

            if( _additionalAudioControllers != null )
            {
                for( int index = 0; index < _additionalAudioControllers.Count; index++ )
                {
                    var ac = _additionalAudioControllers[index];
                    for( int i = 0; i < ac.playlists.Length; ++i )
                    {
                        if( playlistName == ac.playlists[i].name )
                            return ac.playlists[i];
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Gets a copy of the playlist audioID array with the specified name
        /// </summary>
        /// <returns>
        /// The playlist audio item ID array
        /// </returns>
        static public string[] GetPlaylist( string playlistName )
        {
            var  pl = Instance.GetPlaylistByName( playlistName );
            if( pl == null ) return null;

            string[] playlistCopy = new string[pl.playlistItems != null ? pl.playlistItems.Length : 0];

            if( playlistCopy.Length > 0 )
            {
                Array.Copy( pl.playlistItems, playlistCopy, playlistCopy.Length );
            }
            return playlistCopy;
        }

        /// <summary>
        /// Sets the current playlist of the specified channel to the audioID array
        /// </summary>
        /// <param name="playlistName">The new playlist array</param>
        static public bool SetCurrentPlaylist( string playlistName, AudioChannelType channel = AudioChannelType.Music )
        {
            var playlist = Instance.GetPlaylistByName( playlistName );
            if( playlist == null )
            {
                Debug.LogError( "Playlist with name " + playlistName + " not found" );
                return false;
            }
            var channelData = _GetAudioChannelInternal( channel );
            channelData.SetCurrentPlaylist( playlist );
            return true;
        }

        /// <summary>
        /// Start playing the playlist on the specified channel.
        /// </summary>
        /// <returns>
        /// The <c>AudioObject</c> of the current channel audio, or <c>null</c> if no track could be played.
        /// </returns>
        static public AudioObject PlayPlaylist( string playlistName = null, AudioChannelType channel = AudioChannelType.Music )
        {
            if( !string.IsNullOrEmpty( playlistName ) )
            {
                if( !SetCurrentPlaylist( playlistName, channel ) ) return null;
            }  else
            {
                if( Instance.playlists.Length > 0 )
                {
                    SetCurrentPlaylist( Instance.playlists[0].name );
                }
            }

            return Instance._PlayPlaylist( channel );
        }

        /// <summary>
        /// Jumps to the next the music track on the playlist.
        /// </summary>
        /// <remarks>
        /// If shuffling is enabled it will jump to the next randomly chosen track.
        /// </remarks>
        /// <returns>
        /// The <c>AudioObject</c> of the current music, or <c>null</c> if no music track could be played.
        /// </returns>
        static public AudioObject JumpToNextOnPlaylist( AudioChannelType channel = AudioChannelType.Music )
        {
            var channelData = GetAudioChannel( channel );
            if( channelData.IsPlaylistPlaying() )
            {
                return Instance._PlayNextTrackOnPlaylist( channel, 0 );
            }
            else
                return null;
        }

        /// <summary>
        /// Jumps to the previous music track on the playlist.
        /// </summary>
        /// <remarks>
        /// If shuffling is enabled it will jump to the previously played track.
        /// </remarks>
        /// <returns>
        /// The <c>AudioObject</c> of the current music, or <c>null</c> if no music track could be played.
        /// </returns>
        static public AudioObject JumpToPreviousOnPlaylist( AudioChannelType channel = AudioChannelType.Music )
        {
            var channelData = GetAudioChannel( channel );
            if( channelData.IsPlaylistPlaying() )
            {
                return Instance._PlayPreviousTrackOnPlaylist( channel, 0 );
            }
            else
                return null;
        }

        /// <summary>
        /// Determines whether the playlist is playing on the channel.
        /// </summary>
        static public bool IsPlaylistPlaying( AudioChannelType channel = AudioChannelType.Music )
        {
            var channelData = GetAudioChannel( channel );
            return channelData.IsPlaylistPlaying();
        }

        /// <summary>
        /// Clears all music playlist.
        /// </summary>
        static public void ClearPlaylists()
        {
            Instance.playlists = null;
        }

        /// <summary>
        /// Adds a new playlist.
        /// </summary>
        /// <param name="playlistName">The name of the playlist to be added</param>
        /// <param name="audioItemIDs">A list of audio item IDs that will represent the playlist</param>
        static public void AddPlaylist( string playlistName, string[] audioItemIDs )
        {
            var pl = new Playlist( playlistName, audioItemIDs );
            ArrayHelper.AddArrayElement( ref Instance.playlists, pl );
        }

        /// <summary>
        /// Plays an audio item with the name <c>audioID</c>.
        /// </summary>
        /// <param name="audioID">The audio ID.</param>
        /// <param name="channel">The <see cref="AudioChannelType"/>to play the audio on [default=0]</param>
        /// <returns>
        /// Returns the reference of the AudioObject that is used to play the audio item, or <c>null</c> if the audioID does not exist.
        /// Warning: Use <see cref="PoolableReference{T}"/> to store an AudioObject reference if you have pooling enabled.
        /// </returns>
        /// <remarks>
        /// If "3D sound" is enabled in the audio import settings of the audio clip the object will be placed right 
        /// in front of the current audio listener which is usually on the main camera. Note that the audio object will not
        /// be parented - so you will hear when the audio listener moves.
        /// </remarks>
        static public AudioObject Play( string audioID, AudioChannelType channel = AudioChannelType.Default )
        {
            if( channel != AudioChannelType.Default )
            {
                return Instance._PlayInSingleTrackChannel( audioID, channel, 1, 0, 0 );
            }

            AudioListener al = GetCurrentAudioListener();

            if( al == null )
            {
                Debug.LogWarning( "No AudioListener found in the scene" );
                return null;
            }

            return Play( audioID, al.transform.position + al.transform.forward, null, 1, 0, 0, channel );
        }

        /// <summary>
        /// Plays an audio item with the name <c>audioID</c>.
        /// </summary>
        /// <param name="audioID">The audio ID.</param>
        /// <param name="volume">The volume between 0 and 1 [default=1].</param>
        /// <param name="delay">The delay [default=0].</param>
        /// <param name="startTime">The start time [default=0]</param>
        /// <param name="channel">The <see cref="AudioChannelType"/>to play the audio on [default=0]</param>
        /// <returns>
        /// Returns the reference of the AudioObject that is used to play the audio item, or <c>null</c> if the audioID does not exist.
        /// Warning: Use <see cref="PoolableReference{T}"/> to store an AudioObject reference if you have pooling enabled.
        /// </returns>
        /// <remarks>
        /// If "3D sound" is enabled in the audio import settings of the audio clip the object will be placed right 
        /// in front of the current audio listener which is usually on the main camera. Note that the audio object will not
        /// be parented - so you will hear when the audio listener moves.
        /// </remarks>
        static public AudioObject Play( string audioID, float volume, float delay = 0, float startTime = 0, AudioChannelType channel = AudioChannelType.Default )
        {
            if( channel != AudioChannelType.Default )
            {
                return Instance._PlayInSingleTrackChannel( audioID, channel, volume, delay, startTime );
            }

            AudioListener al = GetCurrentAudioListener();

            if( al == null )
            {
                Debug.LogWarning( "No AudioListener found in the scene" );
                return null;
            }

            return Play( audioID, al.transform.position + al.transform.forward, null, volume, delay, startTime, channel );
        }

        /// <summary>
        /// Plays an audio item with the name <c>audioID</c> parented to a specified transform.
        /// </summary>
        /// <param name="audioID">The audio ID.</param>
        /// <param name="parentObj">The parent transform.</param>
        /// <param name="channel">The <see cref="AudioChannelType"/>to play the audio on [default=0]</param>
        /// <returns>
        /// Returns the reference of the AudioObject that is used to play the audio item, or <c>null</c> if the audioID does not exist.
        /// </returns>
        /// <remarks>
        /// If the audio clip is marked as 3D the audio clip will be played at the position of the parent transform. 
        /// As the audio object will get attached to the transform, it is important to destroy the parent object using the
        /// <see cref="ObjectPoolController.Destroy"/> function, even if the parent object is not poolable itself
        /// </remarks>
        static public AudioObject Play( string audioID, Transform parentObj, AudioChannelType channel = AudioChannelType.Default )
        {
            return Play( audioID, parentObj.position, parentObj, 1, 0, 0, channel );
        }

        /// <summary>
        /// Plays an audio item with the name <c>audioID</c> parented to a specified transform.
        /// </summary>
        /// <param name="audioID">The audio ID.</param>
        /// <param name="parentObj">The parent transform.</param>
        /// <param name="volume">The volume between 0 and 1 [default=1].</param>
        /// <param name="delay">The delay [default=0].</param>
        /// <param name="startTime">The start time [default=0]</param>
        /// <param name="channel">The <see cref="AudioChannelType"/>to play the audio on [default=0]</param>
        /// <returns>
        /// Returns the reference of the AudioObject that is used to play the audio item, or <c>null</c> if the audioID does not exist.
        /// </returns>
        /// <remarks>
        /// If the audio clip is marked as 3D the audio clip will be played at the position of the parent transform. 
        /// As the audio object will get attached to the transform, it is important to destroy the parent object using the
        /// <see cref="ObjectPoolController.Destroy"/> function, even if the parent object is not poolable itself
        /// </remarks>
        static public AudioObject Play( string audioID, Transform parentObj, float volume, float delay = 0, float startTime = 0, AudioChannelType channel = AudioChannelType.Default )
        {
            return Play( audioID, parentObj.position, parentObj, volume, delay, startTime, channel );
        }

        /// <summary>
        /// Plays an audio item with the name <c>audioID</c> parented to a specified transform with a world offset.
        /// </summary>
        /// <param name="audioID">The audio ID.</param>
        /// <param name="worldPosition">The position in world coordinates.</param>
        /// <param name="parentObj">The parent transform [default=null]. </param>
        /// <param name="channel">The <see cref="AudioChannelType"/>to play the audio on [default=0]</param>
        /// <returns>
        /// Returns the reference of the AudioObject that is used to play the audio item, or <c>null</c> if the audioID does not exist.
        /// </returns>
        /// <remarks>
        /// If the audio clip is marked as 3D the audio clip will be played at the position of the parent transform. 
        /// As the audio object will get attached to the transform, it is important to destroy the parent object using the
        /// <see cref="ObjectPoolController.Destroy"/> function, even if the parent object is not poolable itself
        /// </remarks>
        static public AudioObject Play( string audioID, Vector3 worldPosition, Transform parentObj = null, AudioChannelType channel = AudioChannelType.Default )
        {
            //Debug.Log( "Play: '" + audioID + "'" );
            return Instance._PlayInChannel( audioID, channel, 1, worldPosition, parentObj, 0, 0, false );
        }

        /// <summary>
        /// Plays an audio item with the name <c>audioID</c> parented to a specified transform with a world offset.
        /// </summary>
        /// <param name="audioID">The audio ID.</param>
        /// <param name="worldPosition">The position in world coordinates.</param>
        /// <param name="parentObj">The parent transform.</param>
        /// <param name="volume">The volume between 0 and 1 [default=1].</param>
        /// <param name="delay">The delay [default=0].</param>
        /// <param name="startTime">The start time [default=0]</param>
        /// <param name="channel">The <see cref="AudioChannelType"/>to play the audio on [default=0]</param>
        /// <returns>
        /// Returns the reference of the AudioObject that is used to play the audio item, or <c>null</c> if the audioID does not exist.
        /// </returns>
        /// <remarks>
        /// If the audio clip is marked as 3D the audio clip will be played at the position of the parent transform. 
        /// As the audio object will get attached to the transform, it is important to destroy the parent object using the
        /// <see cref="ObjectPoolController.Destroy"/> function, even if the parent object is not poolable itself
        /// </remarks>
        static public AudioObject Play( string audioID, Vector3 worldPosition, Transform parentObj, float volume, float delay = 0, float startTime = 0, AudioChannelType channel = AudioChannelType.Default )
        {
            //Debug.Log( "Play: '" + audioID + "'" );
            return Instance._PlayInChannel( audioID, channel, volume, worldPosition, parentObj, delay, startTime, false );
        }

        /// <summary>
        /// Plays an audio item with the name <c>audioID</c> parented to a specified transform with a world offset scheduled at a specified high precision DSP time
        /// (see the Unity AudioSettings.dspTime documentation)
        /// </summary>
        /// <param name="dspTime">The high precision DSP time at which to start playing.</param>
        /// <param name="audioID">The audio ID.</param>
        /// <param name="worldPosition">The position in world coordinates.</param>
        /// <param name="parentObj">The parent transform.</param>
        /// <param name="volume">The volume between 0 and 1 [default=1].</param>
        /// <param name="startTime">The start time seconds [default=0]</param>
        /// <param name="channel">The <see cref="AudioChannelType"/>to play the audio on [default=0]</param>
        /// <returns>
        /// Returns the reference of the AudioObject that is used to play the audio item, or <c>null</c> if the audioID does not exist.
        /// </returns>
        static public AudioObject PlayScheduled( string audioID, double dspTime, Vector3 worldPosition, Transform parentObj = null, float volume = 1.0f, double startTime = 0, AudioChannelType channel = AudioChannelType.Default )
        {
            return Instance._PlayInChannel( audioID, channel, volume, worldPosition, parentObj, 0, startTime, false, dspTime );
        }

        /// <summary>
        /// Plays an audio item with the name <c>audioID</c> right after the given <see cref="AudioObject"/> stops playing. 
        /// (see the Unity AudioSettings.dspTime documentation)
        /// </summary>
        /// <param name="audioID">The audio ID.</param>
        /// <param name="playingAudio">Playback will start after this <see cref="AudioObject"/> finished playing </param>
        /// <param name="deltaDspTime">A time delta (high precision DSP time) at which to start playing. Negative values will cause audios to overlap.</param>
        /// <param name="volume">The volume between 0 and 1 [default=1].</param>
        /// <param name="startTime">The start time seconds [default=0]</param>
        /// <param name="channel">The <see cref="AudioChannelType"/>to play the audio on [default=0]</param>
        /// <returns>
        /// Returns the reference of the AudioObject that is used to play the audio item, or <c>null</c> if the audioID does not exist.
        /// </returns>
        /// <remarks>
        /// Uses the PlayScheduled function that allows to stitch two audios together at DSP level precision without a gap. 
        /// Can not be used to chain more then one audio.
        /// </remarks>
        static public AudioObject PlayAfter( string audioID, AudioObject playingAudio, double deltaDspTime = 0, float volume = 1.0f, double startTime = 0, AudioChannelType channel = AudioChannelType.Default )
        {
#if UNITY_AUDIO_FEATURES_4_1
            double dspTime;

            dspTime = AudioSettings.dspTime;

            if( playingAudio.IsPlaying() )
            {
                dspTime += playingAudio.timeUntilEnd;
            }

            dspTime += deltaDspTime;

            return AudioController.PlayScheduled( audioID, dspTime, playingAudio.transform.position, playingAudio.transform.parent, volume, startTime, channel );
#else
        Debug.LogError( "PlayAfter is only supported in Unity v4.1 or newer" );
        return null;
#endif
        }

        /// <summary>
        /// Plays an audio item with the name <c>audioID</c> in high-precision sync with the specified <see cref="AudioObject"/>.
        /// (see the Unity AudioSettings.dspTime documentation)
        /// </summary>
        /// <param name="audioID">The audio ID.</param>
        /// <param name="playingAudio">The AudiObject to play in sync with<see cref="AudioObject"/></param>
        /// <param name="channel">The <see cref="AudioChannelType"/>to play the audio on [default=0]</param>
        /// <param name="deltaDspTime">A time delta (high precision DSP time) at which to start playing.</param>
        /// <param name="volume">The volume between 0 and 1 [default=1].</param>
        /// <param name="timeToAllowUnityToStartAudio">In order to achieve high precision we have to allow Unity some time start playing the audio</param>
        /// <returns>
        /// Returns the reference of the AudioObject that is used to play the audio item, or <c>null</c> if the audioID does not exist.
        /// </returns>
        /// <remarks>
        /// Uses the PlayScheduled function that allows to play audios at DSP level precision starting point. Makes sure that both audio tracks 
        /// are played in sync (time realtive to clip start is the same for both audio clips if deltaDspTime=0)
        /// Can not start immediately, we have to give Unity some time (see timeToAllowUnityToStartAudio param).
        /// </remarks>
        static public AudioObject PlayInSync( string audioID, AudioObject playingAudio, AudioChannelType channel = AudioChannelType.Default, double deltaDspTime = 0, float volume = 1.0f, float timeToAllowUnityToStartAudio = 0.3f )
        {
#if UNITY_AUDIO_FEATURES_4_1
            double startTime = 0;

            if( playingAudio == null )
            {
                Debug.LogError( "playingAudio is null in PlayInSync(...)" );
                return null;
            }

            if( playingAudio.IsPlaying() )
            {
                startTime += playingAudio.pcmTime;
            }

            startTime += deltaDspTime + timeToAllowUnityToStartAudio;

            return AudioController.PlayScheduled( audioID, timeToAllowUnityToStartAudio + AudioSettings.dspTime,
                playingAudio.transform.position, playingAudio.transform.parent, volume, startTime, channel );
#else
        Debug.LogError( "PlayInSync is only supported in Unity v4.1 or newer" );
        return null;
#endif
        }

        /// <summary>
        /// Stops all playing audio items with name <c>audioID</c> with a fade-out.
        /// </summary>
        /// <param name="audioID">The audio ID.</param>
        /// <param name="fadeOutLength">The fade out time. If a negative value is specified, the subitem's <see cref="AudioSubItem.FadeOut"/> value is taken.</param>
        /// <returns>Return <c>true</c> if any audio was stopped.</returns>
        static public bool Stop( string audioID, float fadeOutLength )
        {
            AudioItem sndItem = Instance._GetAudioItem( audioID );

            if( sndItem == null )
            {
                Debug.LogWarning( "Audio item with name '" + audioID + "' does not exist" );
                return false;
            }

            //if ( sndItem.PlayInstead.Length > 0 )
            //{
            //    return Stop( sndItem.PlayInstead, fadeOutLength );
            //}

            List<AudioObject> audioObjs = GetPlayingAudioObjects( audioID );

            for( int index = 0; index < audioObjs.Count; index++ )
            {
                AudioObject audioObj = audioObjs[index];
                if( fadeOutLength < 0 )
                {
                    audioObj.Stop();
                }
                else
                {
                    audioObj.Stop( fadeOutLength );
                }
            }
            return audioObjs.Count > 0;
        }

        /// <summary>
        /// Stops all playing audio items with name <c>audioID</c>.
        /// </summary>
        /// <param name="audioID">The audio ID.</param>
        /// <returns>Return <c>true</c> if any audio was stopped.</returns>
        static public bool Stop( string audioID )
        {
            return AudioController.Stop( audioID, -1 );
        }

        /// <summary>
        /// Fades out all playing audio items (including the music).
        /// </summary>
        /// <param name="fadeOutLength">The fade out time. If a negative value is specified, the subitem's <see cref="AudioSubItem.FadeOut"/> value is taken.</param>
        static public void StopAll( float fadeOutLength )
        {
            for( var i = 0; i < Instance.audioChannels.Length; i++ )
            {
                var c = Instance.audioChannels[i];
                c.Stop( fadeOutLength );
            }

            InvokeForAllPlayingAudioObjects( ( o ) =>
            {
                o.Stop( fadeOutLength );
            } );
        }

        /// <summary>
        /// Immediately stops playing audio items (including the music).
        /// </summary>
        static public void StopAll()
        {
            AudioController.StopAll( -1 );
        }

        /// <summary>
        /// Stops the music AudioChannel.
        /// </summary>
        /// <param name="fadeOutLength">The fade-out time [Default=0]</param>
        [Obsolete("Use StopChannel instead")]
        static public void StopMusic( float fadeOutLength = 0 )
        {
            StopChannel( AudioChannelType.Music );
        }

        /// <summary>
        /// Stops the Ambience AudioChannel.
        /// </summary>
        /// <param name="fadeOutLength">The fade-out time [Default=0]</param>
        [Obsolete( "Use StopChannel instead" )]
        static public void StopAmbienceSound( float fadeOutLength = 0 )
        {
            StopChannel( AudioChannelType.Ambience );
        }

        /// <summary>
        /// Stops the AudioChannel.
        /// </summary>
        /// <param name="fadeOutLength">The fade-out time [Default=0]</param>
        static public void StopChannel( AudioChannelType channel, float fadeOutLength = 0 )
        {
            if( channel == AudioChannelType.Default )
            {
                InvokeForAllPlayingAudioObjects( ( o ) =>
                {
                    if( o.channel == AudioChannelType.Default ) o.Stop( fadeOutLength );
                } );
            }
            else
            {
                var ch = GetAudioChannel( channel );
                ch.Stop( fadeOutLength );
            }
        }

        /// <summary>
        /// Pauses all playing audio items (including the music).
        /// </summary>
        /// <param name="fadeOutLength">The fade-out time [Default=0]</param>
        static public void PauseAll( float fadeOutLength = 0 )
        {
            InvokeForAllPlayingAudioObjects( ( o ) =>
            {
                o.Pause( fadeOutLength );
            } );
        }

        /// <summary>
        /// Pauses the AudioChannel.
        /// </summary>
        /// <param name="fadeOutLength">The fade-out time [Default=0]</param>
        static public void PauseChannel( AudioChannelType channel, float fadeOutLength = 0 )
        {
            if( channel == AudioChannelType.Default )
            {
                InvokeForAllPlayingAudioObjects( ( o ) =>
                {
                    if(o.channel == AudioChannelType.Default ) o.Pause( fadeOutLength );
                } );
            } else
            {
                var ch = GetAudioChannel( channel );
                ch.Pause( fadeOutLength );
            }
        }

        /// <summary>
        /// Unpauses the AudioChannel.
        /// </summary>
        /// <param name="fadeOutLength">The fade-out time [Default=0]</param>
        static public void UnpauseChannel( AudioChannelType channel, float fadeInLength = 0 )
        {
            if( channel == AudioChannelType.Default )
            {
                InvokeForAllPlayingAudioObjects( ( o ) =>
                {
                    if( o.channel == AudioChannelType.Default ) o.Unpause( fadeInLength );
                } );
            }
            else
            {
                var ch = GetAudioChannel( channel );
                if( ch.enabled )
                {
                    ch.Unpause( fadeInLength );
                }
            }
        }

        /// <summary>
        /// Un-pauses all playing audio items (including the music).
        /// </summary>
        /// <param name="fadeInLength">The fade-in time [Default=0]</param>
        static public void UnpauseAll( float fadeInLength = 0 )
        {
            var ac = Instance;

            AudioController.InvokeForAllPlayingAudioObjects( ( o ) =>
            {
                if( o.IsPaused() )
                {
                    if( GetAudioChannel( o.channel ).enabled )
                    { 
                       o.Unpause( fadeInLength );
                    }
                }
            }, true );
        }

        /// <summary>
        /// Pauses all playing audio items in the specified category (including the music).
        /// </summary>
        /// <param name="categoryName">Name of category.</param>
        /// <param name="fadeOutLength">The fade-out time [Default=0]</param>
        static public void PauseCategory( string categoryName, float fadeOutLength = 0 )
        {
            List<AudioObject> objs = GetPlayingAudioObjectsInCategory( categoryName );

            for( int index = 0; index < objs.Count; index++ )
            {
                AudioObject o = objs[index];
                o.Pause( fadeOutLength );
            }
        }

        /// <summary>
        /// Un-pauses all playing audio items in the specified category (including the music).
        /// </summary>
        /// <param name="categoryName">Name of category.</param>
        /// <param name="fadeInLength">The fade-in time [Default=0]</param>
        static public void UnpauseCategory( string categoryName, float fadeInLength = 0 )
        {
            List<AudioObject> objs = GetPlayingAudioObjectsInCategory( categoryName, true );

            for( int index = 0; index < objs.Count; index++ )
            {
                AudioObject o = objs[index];
                if( o.IsPaused() )
                {
                    o.Unpause( fadeInLength );
                }
            }
        }

        /// <summary>
        /// Stops all playing audio items in the specified category (including the music).
        /// </summary>
        /// <param name="categoryName">Name of category.</param>
        /// <param name="fadeOutLength">The fade-out time [Default=0]</param>
        static public void StopCategory( string categoryName, float fadeOutLength = 0 )
        {
            List<AudioObject> objs = GetPlayingAudioObjectsInCategory( categoryName );

            for( int index = 0; index < objs.Count; index++ )
            {
                AudioObject o = objs[index];
                o.Stop( fadeOutLength );
            }
        }
        
	    /// <summary>
        /// Enables the music.
        /// </summary>
        /// <param name="b">if set to <c>true</c> [b].</param>
        [Obsolete( "Use AudioController.GetAudioChannel instead" )]
        static public void EnableMusic( bool b )
        {
            AudioController.GetAudioChannel(AudioChannelType.Music).enabled = b;
        }

        /// <summary>
        /// Enables the ambience sound.
        /// </summary>
        /// <param name="b">if set to <c>true</c> [b].</param>
        [Obsolete( "Use AudioController.GetAudioChannel instead" )]
        static public void EnableAmbienceSound( bool b )
        {
            AudioController.GetAudioChannel( AudioChannelType.Ambience ).enabled = b;

        }

        /// <summary>
        /// Determines whether music is enabled.
        /// </summary>
        /// <returns>
        ///   <c>true</c> if music is enabled; otherwise, <c>false</c>.
        /// </returns>
        [Obsolete( "Use AudioController.GetAudioChannel instead" )]
        static public bool IsMusicEnabled()
        {
            return AudioController.GetAudioChannel( AudioChannelType.Music ).enabled;
        }

        /// <summary>
        /// Determines whether ambience sound is enabled.
        /// </summary>
        /// <returns>
        ///   <c>true</c> if ambience sound is enabled; otherwise, <c>false</c>.
        /// </returns>
        [Obsolete( "Use AudioController.GetAudioChannel instead" )]
        static public bool IsAmbienceSoundEnabled()
        {
            return AudioController.GetAudioChannel( AudioChannelType.Ambience ).enabled;
        }

        /// <summary>
        /// Determines whether the specified audio ID is playing.
        /// </summary>
        /// <param name="audioID">The audio ID.</param>
        /// <returns>
        ///   <c>true</c> if the specified audio ID is playing; otherwise, <c>false</c>.
        /// </returns>
        static public bool IsPlaying( string audioID )
        {
            return GetPlayingAudioObjects( audioID ).Count > 0;
        }

        /// <summary>
        /// Returns an array of all playing audio objects with the specified <c>audioID</c>.
        /// </summary>
        /// <param name="audioID">The audio ID.</param>
        /// <param name="includePausedAudio">If enabled the returned array will also contain paused audios.</param>
        /// <returns>
        /// Array of all playing audio objects with the specified <c>audioID</c>.
        /// </returns>
        static public List<AudioObject> GetPlayingAudioObjects( string audioID, bool includePausedAudio = false )
        {
            return GetPlayingAudioObjects( includePausedAudio, ( o ) =>
                {
                    return o.audioID == audioID;
                } );
        }

        /// <summary>
        /// Returns an array of all playing audio objects in the category with name <c>categoryName</c>.
        /// </summary>
        /// <param name="categoryName">The category name.</param>
        /// <param name="includePausedAudio">If enabled the returned array will also contain paused audios.</param>
        /// <returns>
        /// Array of all playing audio objects belonging to the specified category or one of its child categories.
        /// </returns>
        static public List<AudioObject> GetPlayingAudioObjectsInCategory( string categoryName, bool includePausedAudio = false )
        {
            return GetPlayingAudioObjects( includePausedAudio, ( o ) =>
            {
                return o.DoesBelongToCategory( categoryName );
            } );
        }

        /// <summary>
        /// Returns an array of all playing audio objects.
        /// </summary>
        /// <param name="includePausedAudio">If enabled the returned array will also contain paused audios.</param>
        /// <returns>
        /// Array of all playing audio objects.
        /// </returns>
        static public List<AudioObject> GetPlayingAudioObjects( bool includePausedAudio = false, Predicate<AudioObject> predicate = null )
        {
            var matchesList = new List<AudioObject>();
            RegisteredComponentController.InvokeAllOfType<AudioObject>( ( o ) =>
            {
                if( o == null ) return;
                if( predicate != null )
                {
                    if( !predicate( o ) ) return;
                }
                if( o.IsPlaying() || ( includePausedAudio && o.IsPaused() ) )
                {
                    matchesList.Add( o );
                }
            } );

            return matchesList;
        }

        /// <summary>
        /// Invokes an action for of all playing audio objects.
        /// </summary>
        /// <param name="action">The action to invoke.</param>
        /// <param name="includePausedAudio">If enabled the returned array will also contain paused audios.</param>
        static public void InvokeForAllPlayingAudioObjects( Action<AudioObject> action, bool includePausedAudio = false )
        {
            RegisteredComponentController.InvokeAllOfType<AudioObject>( ( o ) =>
                {
                    if( o == null ) return;
                    if( o.IsPlaying() || ( includePausedAudio && o.IsPaused() ) )
                    {
                        action.Invoke( o );
                    }
                } );
        }

        /// <summary>
        /// Returns the number of all playing audio objects with the specified <c>audioID</c>.
        /// </summary>
        /// <param name="audioID">The audio ID.</param>
        /// <param name="includePausedAudio">If enabled the returned array will also contain paused audios.</param>
        /// <returns>
        /// Number of all playing audio objects with the specified <c>audioID</c>.
        /// </returns>
        static public int GetPlayingAudioObjectsCount( string audioID, bool includePausedAudio = false )
        {
            int count = 0;
            InvokeForAllPlayingAudioObjects( ( o ) =>
            {
                if( o.audioID == audioID )
                {
                    count++;
                }
            }, includePausedAudio );

            return count;
        }

        /// <summary>
        /// Mutes / Unmutes the sound.
        /// </summary>
        /// <remarks>
        /// 'Sound' means all audio except music and ambience sound.
        /// </remarks>
        /// <param name="b">if set to <c>true</c> [b].</param>
        static public void MuteSound( bool b )
        {
            AudioController.Instance.soundMuted = b;
        }

        /// <summary>
        /// Determines whether sound is muted
        /// </summary>
        /// <returns>
        ///   <c>true</c> if sound is muted; otherwise, <c>false</c>.
        /// </returns>
        /// <remarks>
        /// 'Sound' means all audio except music and ambience sound.
        /// </remarks>
        static public bool IsSoundMuted()
        {
            return AudioController.Instance.soundMuted;
        }

        /// <summary>
        /// Gets the currently active Unity audio listener.
        /// </summary>
        /// <returns>
        /// Reference of the currently active AudioListener object.
        /// </returns>
        static public AudioListener GetCurrentAudioListener()
        {
            AudioController MyInstance = Instance;
            if( MyInstance._currentAudioListener != null && MyInstance._currentAudioListener.gameObject == null ) // TODO: check if this is necessary and if it really works if object was destroyed
            {
                MyInstance._currentAudioListener = null;
            }

            if( MyInstance._currentAudioListener == null )
            {
                MyInstance._currentAudioListener = (AudioListener)FindObjectOfType( typeof( AudioListener ) );
            }

            return MyInstance._currentAudioListener;
        }


        /// <summary>
        /// Gets a category.
        /// </summary>
        /// <param name="name">The category's name.</param>
        /// <returns>The category or <c>null</c> if no category with the specified name exists</returns>
        static public AudioCategory GetCategory( string name )
        {
            var primaryInstance = Instance;

            AudioCategory cat = primaryInstance._GetCategory( name );

            if( cat != null )
            {
                return cat;
            }

            if( primaryInstance._additionalAudioControllers != null )
            {
                for( int index = 0; index < primaryInstance._additionalAudioControllers.Count; index++ )
                {
                    var ac = primaryInstance._additionalAudioControllers[index];
                    cat = ac._GetCategory( name );

                    if( cat != null )
                    {
                        return cat;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Changes the category volume. Also effects currently playing audio items.
        /// </summary>
        /// <param name="name">The category name.</param>
        /// <param name="volume">The volume (between 0 and 1).</param>
        static public void SetCategoryVolume( string name, float volume )
        {
            var catList = _GetAllCategories( name );

            if( catList.Count == 0 )
            {
                Debug.LogWarning( "No audio category with name " + name );
                return;
            }

            for( int i = 0; i < catList.Count; i++ )
            {
                catList[i].Volume = volume;
            }
        }

        /// <summary>
        /// Gets the category volume.
        /// </summary>
        /// <param name="name">The category name.</param>
        /// <returns>The volume of the specified category</returns>
        static public float GetCategoryVolume( string name )
        {
            AudioCategory category = GetCategory( name );
            if( category != null )
            {
                return category.Volume;
            }
            else
            {
                Debug.LogWarning( "No audio category with name " + name );
                return 0;
            }
        }

        /// <summary>
        /// Starts a fade-out of an audio category.
        /// </summary>
        /// <param name="name">The category name.</param>
        /// <remarks>
        /// If the category is already fading out the requested fade-out is combined with the existing one.
        /// </remarks>
        /// <param name="fadeOutLength">The fade time in seconds. If a negative value is specified, the fade out as specified in the corresponding <see cref="AudioSubItem.FadeOut"/> is used</param>
        /// <param name="startToFadeTime">Fade out starts after <c>startToFadeTime</c> seconds have passed</param>
        static public void FadeOutCategory( string name, float fadeOutLength, float startToFadeTime = 0 )
        {
            var catList = _GetAllCategories( name );

            if( catList.Count == 0 )
            {
                Debug.LogWarning( "No audio category with name " + name );
                return;
            }

            for( int i = 0; i < catList.Count; i++ )
            {
                catList[i].FadeOut( fadeOutLength, startToFadeTime );
            }
        }

        /// <summary>
        /// Starts a fade-in of an audio category.
        /// </summary>
        /// <param name="name">The category name.</param>
        /// <param name="fadeInTime">The fade time in seconds.</param>
        /// <param name="stopCurrentFadeOut">In case of an existing fade-out this parameter determines if the fade-out is stopped.</param>
        static public void FadeInCategory( string name, float fadeInTime, bool stopCurrentFadeOut = true )
        {
            var catList = _GetAllCategories( name );

            if( catList.Count == 0 )
            {
                Debug.LogWarning( "No audio category with name " + name );
                return;
            }

            for( int i = 0; i < catList.Count; i++ )
            {
                catList[i].FadeIn( fadeInTime, stopCurrentFadeOut );
            }
        }

        /// <summary>
        /// Changes the global volume. Effects all currently playing audio items.
        /// </summary>
        /// <param name="volume">The volume (between 0 and 1).</param>
        /// <remarks>
        /// Volume change is also applied to all additional AudioControllers.
        /// </remarks>
        static public void SetGlobalVolume( float volume )
        {
            var primaryInstance = Instance;
            primaryInstance.Volume = volume;

            if( primaryInstance._additionalAudioControllers != null )
            {
                for( int index = 0; index < primaryInstance._additionalAudioControllers.Count; index++ )
                {
                    var ac = primaryInstance._additionalAudioControllers[index];
                    ac.Volume = volume;
                }
            }
        }

        /// <summary>
        /// Gets the global volume.
        /// </summary>
        /// <returns>
        /// The global volume (between 0 and 1).
        /// </returns>
        static public float GetGlobalVolume()
        {
            return Instance.Volume;
        }

        /// <summary>
        /// Creates a new audio category
        /// </summary>
        /// <param name="categoryName">Name of the category.</param>
        /// <returns>
        /// Reference to the new category.
        /// </returns>
        static public AudioCategory NewCategory( string categoryName )
        {
            // can not use ArrayHelper at this point because of buggy Flash compiler :(

            int oldCategoryCount = Instance.AudioCategories != null ? Instance.AudioCategories.Length : 0;
            var oldArray = Instance.AudioCategories;
            Instance.AudioCategories = new AudioCategory[oldCategoryCount + 1];

            if( oldCategoryCount > 0 )
            {
                oldArray.CopyTo( Instance.AudioCategories, 0 );
            }

            var newCategory = new AudioCategory( Instance );
            newCategory.Name = categoryName;

            Instance.AudioCategories[oldCategoryCount] = newCategory;
            Instance._InvalidateCategories();
            return newCategory;
        }


        /// <summary>
        /// Removes an audio category.
        /// </summary>
        /// <param name="categoryName">Name of the category to remove.</param>
        static public void RemoveCategory( string categoryName )
        {
            int i, index = -1;
            int oldCategoryCount;

            if( Instance.AudioCategories != null )
            {
                oldCategoryCount = Instance.AudioCategories.Length;
            }
            else
                oldCategoryCount = 0;

            for( i = 0; i < oldCategoryCount; i++ )
            {
                if( Instance.AudioCategories[i].Name == categoryName )
                {
                    index = i;
                    break;
                }
            }

            if( index == -1 )
            {
                Debug.LogError( "AudioCategory does not exist: " + categoryName );
                return;
            }

            //ArrayHelper.DeleteArrayElement( ref Instance.AudioCategories, index ); // can not use ArrayHelper because of buggy Flash compiler :(
            {
                var newArray = new AudioCategory[Instance.AudioCategories.Length - 1];

                for( i = 0; i < index; i++ )
                {
                    newArray[i] = Instance.AudioCategories[i];
                }
                for( i = index + 1; i < Instance.AudioCategories.Length; i++ )
                {
                    newArray[i - 1] = Instance.AudioCategories[i];
                }
                Instance.AudioCategories = newArray;
            }

            Instance._InvalidateCategories();
        }

        /// <summary>
        /// Adds a custom audio item to a category.
        /// </summary>
        /// <param name="category">The category.</param>
        /// <param name="audioItem">The audio item.</param>
        /// <example>
        /// <code>
        /// var audioItem = new AudioItem();
        /// audioItem.SubItemPickMode = AudioPickSubItemMode.Sequence;
        /// 
        /// var subItem0 = new AudioSubItem();
        /// subItem0.Clip = audioClip0;
        /// subItem0.Volume = 0.7f;
        /// audioItem.AddAudioSubitem( subItem0 );
        /// 
        /// var subItem1 = new AudioSubItem();
        /// subItem1.Clip = audioClip1;
        /// subItem1.Volume = 0.8f;
        /// audioItem.AddAudioSubitem( subItem1 );
        /// 
        /// AddToCategory( GetCategory( "CustomSFX" ), audioItem );
        /// </code>
        /// </example>
        /// <seealso cref="AudioController.NewCategory(string)"/>
        /// <seealso cref="AudioController.GetCategory(string)"/>
        static public void AddToCategory( AudioCategory category, AudioItem audioItem )
        {
            category.AddAudioItem( audioItem );
        }

        /// <summary>
        /// Creates an AudioItem with the name <c>audioID</c> containing a single subitem playing the specified 
        /// custom AudioClip. This AudioItem is then added to the specified category.
        /// </summary>
        /// <param name="category">The category.</param>
        /// <param name="audioClip">The custom audio clip.</param>
        /// <param name="audioID">The audioID for the AudioItem to create.</param>
        /// <returns>The <see cref="AudioItem"/> created with the specified <c>audioID</c></returns>
        /// <seealso cref="AudioController.NewCategory(string)"/>
        /// <seealso cref="AudioController.GetCategory(string)"/>
        static public AudioItem AddToCategory( AudioCategory category, AudioClip audioClip, string audioID )
        {
            var audioItem = new AudioItem();
            audioItem.Name = audioID;
            var audioSubItem = new AudioSubItem();
            audioSubItem.Clip = audioClip;
            audioItem.AddAudioSubItem( audioSubItem );
            category.AddAudioItem( audioItem );
            return audioItem;
        }

        /// <summary>
        /// Removes an AudioItem from the AudioController.
        /// </summary>
        /// <param name="audioID">Name of the audio item to remove.</param>
        /// <returns><c>true</c> if the audio item was found and successfully removed, otherwise <c>false</c></returns>
        static public bool RemoveAudioItem( string audioID )
        {
            var audioItem = Instance._GetAudioItem( audioID );

            if( audioItem != null )
            {
                int index = audioItem.category._GetIndexOf( audioItem );
                if( index < 0 ) return false; // should never be the case!

                var array = audioItem.category.AudioItems;

                //ArrayHelper.DeleteArrayElement( audioItem.category, index ); // Flash export does not currently work!! 
                {
                    var newArray = new AudioItem[array.Length - 1];
                    int i;
                    for( i = 0; i < index; i++ )
                    {
                        newArray[i] = array[i];
                    }
                    for( i = index + 1; i < array.Length; i++ )
                    {
                        newArray[i - 1] = array[i];
                    }
                    audioItem.category.AudioItems = newArray;
                }

                if( Instance._categoriesValidated )
                {
                    Instance._audioItems.Remove( audioID );
                }
                return true;
            }
            else
                return false;
        }

        /// <summary>
        /// Tests if a given <c>audioID</c> is valid.
        /// </summary>
        /// <param name="audioID">The audioID</param>
        /// <returns><c>true</c> if the <c>audioID</c> is valid</returns>
        static public bool IsValidAudioID( string audioID )
        {
            return Instance._GetAudioItem( audioID ) != null;
        }

        /// <summary>
        /// Returns the <see cref="AudioItem"/> with the given <c>audioID</c>.
        /// </summary>
        /// <param name="audioID">The <c>audioID</c></param>
        /// <returns>The <see cref="AudioItem"/> if <c>audioID</c> is valid, else <c>null</c> </returns>
        static public AudioItem GetAudioItem( string audioID )
        {
            return Instance._GetAudioItem( audioID );
        }

        /// <summary>
        /// Detaches all audio objects possibly parented to the specified game object.
        /// </summary>
        /// <param name="gameObjectWithAudios">The GameObject with possibly playing AudioObjects.</param>
        /// <remarks>
        /// Use this method on a game object BEFORE destryoing it if you want to keep any audios playing 
        /// parented to this object.
        /// </remarks>
        static public void DetachAllAudios( GameObject gameObjectWithAudios )
        {
            var audioObjs = gameObjectWithAudios.GetComponentsInChildren<AudioObject>( true );
            for( int index = 0; index < audioObjs.Length; index++ )
            {
                var a = audioObjs[index];
                a.transform.parent = null;
            }
        }

        /// <summary>
        /// Gets the audio item's max distance. (respects all proper default values and overwrites).
        /// </summary>
        /// <param name="audioID">The <c>audioID</c></param>
        /// <returns>The max distance applied to the AudioSource</returns>
        static public float GetAudioItemMaxDistance( string audioID )
        {
            var audioItem = AudioController.GetAudioItem( audioID );

            if( audioItem.overrideAudioSourceSettings ) return audioItem.audioSource_MaxDistance;
            else
            {
                return audioItem.category.GetAudioObjectPrefab().GetComponent<AudioSource>().maxDistance;
            }
        }

        /// <summary>
        /// Unloads all AudioClips specified in this AudioController from memory. 
        /// </summary>
        /// <remarks>
        /// You will still be able to play the AudioClips, but you may experience performance hickups when Unity reloads the audio asset
        /// </remarks>

        public void UnloadAllAudioClips()
        {
            for( int index = 0; index < AudioCategories.Length; index++ )
            {
                var c = AudioCategories[index];
                c.UnloadAllAudioClips();
            }
        }

        // **************************************************************************************************/
        //          private / protected functions and properties
        // **************************************************************************************************/

        protected AudioListener _currentAudioListener = null;

        private bool _soundMuted = false;
        private bool _categoriesValidated = false;

        [SerializeField]
        private bool _isAdditionalAudioController = false;

        [SerializeField]
        private bool _audioDisabled = false;

        Dictionary<string, AudioItem> _audioItems;

        [SerializeField]
        private float _volume = 1.0f;

        static private double _systemTime;

        private void _ApplyVolumeChange()
        {
            InvokeForAllPlayingAudioObjects( ( o ) =>
            {
                o._ApplyVolumeBoth();
            }, true );
        }

        internal AudioItem _GetAudioItem( string audioID )
        {
            AudioItem sndItem;

            _ValidateCategories();

            if( _audioItems.TryGetValue( audioID, out sndItem ) )
            {
                return sndItem;
            }

            return null;
        }

        protected AudioObject _PlayInSingleTrackChannel( string audioID, AudioChannelType channel, float volume, double delay, double startTime, double dspTime = 0 )
        {
            var parent = AudioController.GetAudioChannel( channel ).parent;
            if( parent == null )
            {
                AudioListener al = GetCurrentAudioListener();
                if( al == null )
                {
                    Debug.LogWarning( "No AudioListener found in the scene" );
                    return null;
                }
                return _PlayInSingleTrackChannel( audioID, channel , al.transform.position + al.transform.forward, null, volume, delay, startTime, dspTime );
            }
            else
            {
                return _PlayInSingleTrackChannel( audioID, channel, parent.position, parent, volume, delay, startTime, dspTime );
            }
        }

        protected AudioObject _PlayInSingleTrackChannel( string audioID, AudioChannelType channel, Vector3 position, Transform parentObj, float volume, double delay, double startTime, double dspTime = 0 )
        {
            var channelInfos = _GetAudioChannelInternal( channel );
            channelInfos.isPlaylistPlaying = false;

            if( !channelInfos.enabled ) return null;

            bool doFadeIn;

            if( channelInfos.currentlyPlaying != null )
            {
                doFadeIn = channelInfos.currentlyPlaying.IsPlaying();
                channelInfos.Stop( channelInfos.settings.crossfadeTime_Out );
            }
            else
                doFadeIn = false;

            //Debug.Log( "PlayMusic " + audioID );

            if( channelInfos.settings.crossfadeTime_In <= 0 ) doFadeIn = false;

            channelInfos.currentlyPlaying = _PlayWithLogAndChecks( audioID, channel, volume, position, parentObj, delay, startTime, false, dspTime, null, doFadeIn ? 0 : 1 );

            if( doFadeIn && channelInfos.currentlyPlaying )
            {
                channelInfos.currentlyPlaying.FadeIn( channelInfos.settings.crossfadeTime_In );
            }

            return channelInfos.currentlyPlaying;
        }

        private int _EnqueueOnChannel( string audioID, AudioChannelType channel )
        {
            int newLength;

            Playlist currentPl = _GetCurrentPlaylist( channel );

            if( currentPl == null )
            {
                newLength = 1;
            }
            else
                newLength = playlists.Length + 1;

            string[] newPlayList = new string[newLength];

            if( currentPl != null )
            {
                currentPl.playlistItems.CopyTo( newPlayList, 0 );
            }

            newPlayList[newLength - 1] = audioID;
            currentPl.playlistItems = newPlayList;

            return newLength;
        }

        protected AudioObject _PlayPlaylist( AudioChannelType channel )
        {
            var channelData = _GetAudioChannelInternal( channel );
            channelData.ClearPlaylistPlayed();
            return _PlayNextTrackOnPlaylist( channel, 0 );
        }

        private AudioObject _PlayPlaylistTrackWithID( AudioChannelType channel, int nextTrack, float delay, bool addToPlayedList )
        {
            if( nextTrack < 0 )
            {
                return null;
            }
            
            //Debug.Log( "nextTrack: " + nextTrack );
            var pl = _GetCurrentPlaylist( channel );
            if( nextTrack >= pl.playlistItems.Length ) return null;
            AudioObject audioObj = _PlayInSingleTrackChannel( pl.playlistItems[nextTrack], channel, 1, delay, 0 );

            if( audioObj != null )
            {
                audioObj._isCurrentPlaylistTrack = true;
                audioObj.primaryAudioSource.loop = false;

                var channelData = AudioController._GetAudioChannelInternal( channel );

                channelData.playlistPlayed.Add( nextTrack );
                channelData.isPlaylistPlaying = true;
            }
            return audioObj;
        }

        internal AudioObject _PlayNextTrackOnPlaylist( AudioChannelType channel, float delay )
        {
            var channelData = _GetAudioChannelInternal( channel );
            int nextTrack = channelData._GetNextTrack();
            return _PlayPlaylistTrackWithID( channel, nextTrack, delay, true );
        }

        internal AudioObject _PlayPreviousTrackOnPlaylist( AudioChannelType channel, float delay )
        {
            var channelData = _GetAudioChannelInternal( channel );
            int nextTrack = channelData._GetPreviousTrack();
            return _PlayPlaylistTrackWithID( channel, nextTrack, delay, false );
        }

        protected AudioObject _PlayInChannel( string audioID, AudioChannelType channel, float volume, Vector3 worldPosition, Transform parentObj, double delay, double startTime, bool playWithoutAudioObject, double dspTime = 0, AudioObject useExistingAudioObject = null, float startVolumeMultiplier = 1 )
        {
            if( _audioDisabled ) return null;

            if( channel != AudioChannelType.Default )
            {
                return _PlayInSingleTrackChannel( audioID, channel, worldPosition, parentObj, volume, delay, startTime, dspTime );
            }
            //Debug.Log( "AudioController Play: " + audioID );

            return _PlayWithLogAndChecks( audioID, channel, volume, worldPosition, parentObj, delay, startTime, playWithoutAudioObject, dspTime, useExistingAudioObject, startVolumeMultiplier );
        }

        protected AudioObject _PlayWithLogAndChecks( string audioID, AudioChannelType channel, float volume, Vector3 worldPosition, Transform parentObj, double delay, double startTime, bool playWithoutAudioObject, double dspTime = 0, AudioObject useExistingAudioObject = null, float startVolumeMultiplier = 1 )
        { 
            AudioItem sndItem = _GetAudioItem( audioID );
            if( sndItem == null )
            {
                Debug.LogWarning( "Audio item with name '" + audioID + "' does not exist" );
                return null;
            }

            if( sndItem._lastPlayedTime > 0 && dspTime == 0 )
            {
                double deltaT = AudioController.systemTime - sndItem._lastPlayedTime;

                if( deltaT < sndItem.MinTimeBetweenPlayCalls )
                {

#if UNITY_EDITOR && !AUDIO_TOOLKIT_DEMO
                    var logData = new AudioLog.LogData_SkippedPlay();
                    logData.audioID = audioID;
                    if( sndItem != null && sndItem.category != null )
                    {
                        logData.category = sndItem.category.Name;
                    }
                    logData.delay = (float)delay;
                    logData.parentObjectName = parentObj != null ? parentObj.name : "";
                    logData.position = worldPosition;
                    logData.startTime = (float)startTime;
                    logData.volume = volume;

                    logData.reasonForSkip = string.Format( "{0:0.00}", deltaT ) + " < MinTimeBetweenPlay";

#if UNITY_AUDIO_FEATURES_4_1
                    if( dspTime > 0 )
                    {
                        logData.scheduledDspTime = Time.time + (float)( dspTime - AudioSettings.dspTime );
                    }
#endif

                    AudioLog.Log( logData );
#endif

                    return null;
                }
            }

            if( sndItem.MaxInstanceCount > 0 )
            {
                var playingAudios = GetPlayingAudioObjects( audioID );  // TODO: check performance of GetPlayingAudioObjects

                bool isExceeding = playingAudios.Count >= sndItem.MaxInstanceCount;

                if( isExceeding )
                {
                    bool isExceedingByMoreThanOne = playingAudios.Count > sndItem.MaxInstanceCount;

                    // search oldest audio and stop it.
                    AudioObject oldestAudio = null;

                    for( int i = 0; i < playingAudios.Count; i++ )
                    {
                        if( !isExceedingByMoreThanOne )
                        {
                            if( playingAudios[i].isFadingOut ) continue;
                        }
                        if( oldestAudio == null || playingAudios[i].startedPlayingAtTime < oldestAudio.startedPlayingAtTime )
                        {
                            oldestAudio = playingAudios[i];
                        }
                    }
                    //oldestAudio.DestroyAudioObject(); // produces cracking noise

                    if( oldestAudio != null )
                    {
                        oldestAudio.Stop( isExceedingByMoreThanOne ? 0 : 0.2f );
                    }

                }
            }

            return PlayAudioItem( sndItem, volume, worldPosition, parentObj, delay, startTime, playWithoutAudioObject, useExistingAudioObject, dspTime, channel, startVolumeMultiplier );
        }

        /// <summary>
        /// Plays a specific AudioItem.
        /// </summary>
        /// <remarks>
        /// This function is used by the editor extension and is normally not required for application developers. 
        /// Use <see cref="AudioController.Play(string)"/> instead.
        /// </remarks>
        /// <param name="sndItem">the AudioItem</param>
        /// <param name="volume">the volume</param>
        /// <param name="worldPosition">the world position </param>
        /// <param name="parentObj">the parent object, or <c>null</c></param>
        /// <param name="delay">the delay in seconds</param>
        /// <param name="startTime">the start time seconds</param>
        /// <param name="playWithoutAudioObject">if <c>true</c>plays the audio by using the Unity 
        /// function <c>PlayOneShot</c> without creating an audio game object. Allows playing audios from within the Unity inspector.
        /// </param>
        /// <param name="useExistingAudioObj">if specified this existing audio object is used instead of creating a new <see cref="AudioObject"/></param>
        /// <param name="dspTime">The high precision DSP time at which to schedule playing the audio [default=0]</param>
        /// <param name="playAsMusicOrAmbienceSound">Determines if it is effected by sound muting [default=false]</param>
        /// <param name="startVolumeMultiplier">allows to adjust the start volume if e.g. a FadeOut will follow immediately after</param>
        /// <returns>
        /// The created <see cref="AudioObject"/> or <c>null</c>
        /// </returns>
        public AudioObject PlayAudioItem( AudioItem sndItem, float volume, Vector3 worldPosition, Transform parentObj = null, double delay = 0, double startTime = 0, bool playWithoutAudioObject = false, AudioObject useExistingAudioObj = null, double dspTime = 0, AudioChannelType channel = AudioChannelType.Default, float startVolumeMultiplier = 1 )
        {
            AudioObject audioObj = null;

            //Debug.Log( "PlayAudioItem '" + sndItem.Name + "'" );

            sndItem._lastPlayedTime = AudioController.systemTime;

            AudioSubItem[] sndSubItems = AudioControllerHelper._ChooseSubItems( sndItem, useExistingAudioObj );

            if( sndSubItems == null || sndSubItems.Length == 0 )
            {
                return null;
            }

            for( int index = 0; index < sndSubItems.Length; index++ )
            {
                var sndSubItem = sndSubItems[index];
                if( sndSubItem != null )
                {
                    var audioObjRet = PlayAudioSubItem( sndSubItem, volume, worldPosition, parentObj, delay, startTime, playWithoutAudioObject, useExistingAudioObj, dspTime, channel, startVolumeMultiplier );

                    if( audioObjRet )
                    {
                        audioObj = audioObjRet;
                        audioObj.audioID = sndItem.Name;

                        audioObjRet._audioSource_Pan_Saved = audioObjRet.primaryAudioSource.panStereo;

                        if( sndItem.overrideAudioSourceSettings )
                        {
                            audioObjRet._audioSource_MinDistance_Saved = audioObjRet.primaryAudioSource.minDistance;
                            audioObjRet._audioSource_MaxDistance_Saved = audioObjRet.primaryAudioSource.maxDistance;
#if UNITY_5_OR_NEWER
                            audioObjRet._audioSource_SpatialBlend_Saved = audioObjRet.primaryAudioSource.spatialBlend;
#endif
                            audioObjRet.primaryAudioSource.minDistance = sndItem.audioSource_MinDistance;
                            audioObjRet.primaryAudioSource.maxDistance = sndItem.audioSource_MaxDistance;
#if UNITY_5_OR_NEWER
                            audioObjRet.primaryAudioSource.spatialBlend = sndItem.spatialBlend;
#endif

                            if( audioObjRet.secondaryAudioSource != null )
                            {
                                audioObjRet.secondaryAudioSource.minDistance = sndItem.audioSource_MinDistance;
                                audioObjRet.secondaryAudioSource.maxDistance = sndItem.audioSource_MaxDistance;
#if UNITY_5_OR_NEWER
                                audioObjRet.secondaryAudioSource.spatialBlend = sndItem.spatialBlend;
#endif
                            }
                        }
                    }
                }
            }

            return audioObj;
        }

        internal AudioCategory _GetCategory( string name )
        {
            for( int index = 0; index < AudioCategories.Length; index++ )
            {
                AudioCategory cat = AudioCategories[index];
                if( cat.Name == name )
                {
                    return cat;
                }
            }
            return null;
        }

        private static double _lastSystemTime = -1;
        private static double _systemDeltaTime = -1;

        void Update()
        {
            if( !_isAdditionalAudioController )
            {
                _UpdateSystemTime();
            }
        }

        static private void _UpdateSystemTime()
        {
            double newSystemTime = SystemTime.timeSinceLaunch;

            if( _lastSystemTime >= 0 )
            {
                _systemDeltaTime = newSystemTime - _lastSystemTime;
                if( _systemDeltaTime > Time.maximumDeltaTime + 0.01f )
                {
                    _systemDeltaTime = Time.deltaTime;
                }
                _systemTime += _systemDeltaTime;
            }
            else
            {
                _systemDeltaTime = 0;
                _systemTime = 0;
            }
            _lastSystemTime = newSystemTime;
        }


#if AUDIO_TOOLKIT_DEMO
    protected virtual void Awake()
    {
#else
        protected override void Awake()
        {
            base.Awake();
#endif
            if( Persistent )
            {
                DontDestroyOnLoad( gameObject );
            }

            // all initialisation must be done in AwakeSingleton()
        }

        static List<AudioController> _additionalControllerToRegister;

        void OnEnable()
        {
            if( isAdditionalAudioController )
            {
                if( AudioController.DoesInstanceExist() )
                {
                    AudioController.Instance._RegisterAdditionalAudioController( this );
                }
                else
                {
                    if( _additionalControllerToRegister == null )
                    {
                        _additionalControllerToRegister = new List<AudioController>();
                    }
                    _additionalControllerToRegister.Add( this );
                }
            }
            else
            {
                if( _additionalControllerToRegister != null )
                {
                    for( int i = 0; i < _additionalControllerToRegister.Count; i++ )
                    {
                        var ac = _additionalControllerToRegister[i];
                        if( ac && ac.enabled )
                        {
                            AudioController.Instance._RegisterAdditionalAudioController( ac );
                        }
                    }
                    _additionalControllerToRegister = null;
                }
            }
        }

        void OnDisable()
        {
            if( isAdditionalAudioController && AudioController.DoesInstanceExist() )
            {
                AudioController.Instance._UnregisterAdditionalAudioController( this );
            }
        }

        /// <summary>
        /// returns <c>true </c>if the AudioController is the main controller (not an additional controller)
        /// </summary>
#if AUDIO_TOOLKIT_DEMO
    public virtual
#else
        public override
#endif
    bool IsSingletonObject
        {
            get
            {
                return !_isAdditionalAudioController;
            }
        }

#if AUDIO_TOOLKIT_DEMO
    protected virtual
#else
        protected override
#endif
    void OnDestroy()
        {
            if( UnloadAudioClipsOnDestroy )
            {
                UnloadAllAudioClips();
            }
#if !AUDIO_TOOLKIT_DEMO
            base.OnDestroy();
#endif
        }

        void AwakeSingleton() // is called by singleton, can be called before Awake() 
        {
            _UpdateSystemTime();

            //Debug.Log( "AwakeSingleton" );

            if( AudioObjectPrefab == null )
            {
                Debug.LogError( "No AudioObject prefab specified in AudioController. To make your own AudioObject prefab create an empty game object, add Unity's AudioSource, the AudioObject script, and the PoolableObject script (if pooling is wanted ). Then create a prefab and set it in the AudioController." );
            }
            else
            {
                _ValidateAudioObjectPrefab( AudioObjectPrefab );
            }
            _ValidateCategories();
        }

        protected bool _AnyAudioControllersWithInvalidatedCategories()
        {
            if( !_categoriesValidated ) return true;
            if( _additionalAudioControllers == null ) return false;
            for( var i = 0; i < _additionalAudioControllers.Count; i++ )
            {
                var ac = _additionalAudioControllers[i];
                if( !_categoriesValidated ) return true;
            }
            return false;
        }

        protected void _ValidateCategoriesInAllAudioControllers()
        {
            _categoriesValidated = true;
            if( _additionalAudioControllers == null ) return;
            for( var i = 0; i < _additionalAudioControllers.Count; i++ )
            {
                var ac = _additionalAudioControllers[i];
                ac._categoriesValidated = true;
            }
        }

        protected void _ValidateCategories()
        {
            if( _AnyAudioControllersWithInvalidatedCategories() )
            {
                InitializeAudioItems();

                _ValidateCategoriesInAllAudioControllers();
            }
        }

        internal void _InvalidateCategories()
        {
            _categoriesValidated = false;
        }

        /// <summary>
        /// Updates the internal <c>audioID</c> dictionary and initializes all registered <see cref="AudioItem"/> objects.
        /// </summary>
        /// <remarks>
        /// There is no need to call this function manually, unless <see cref="AudioItem"/> objects or categories are changed at runtime.
        /// </remarks>
        public void InitializeAudioItems()
        {
            if( isAdditionalAudioController )
            {
                return;
            }

            _audioItems = new Dictionary<string, AudioItem>();

            _InitializeAudioItems( this );
            if( _additionalAudioControllers != null )
            {
                for( int index = 0; index < _additionalAudioControllers.Count; index++ )
                {
                    var ac = _additionalAudioControllers[index];
                    if( ac != null )
                    {
                        _InitializeAudioItems( ac );
                    }
                }
            }
        }

        private void _InitializeAudioItems( AudioController audioController )
        {
            for( int index = 0; index < audioController.AudioCategories.Length; index++ )
            {
                AudioCategory category = audioController.AudioCategories[index];
                category.audioController = audioController;
                category._AnalyseAudioItems( _audioItems );

                if( category.AudioObjectPrefab )
                {
                    _ValidateAudioObjectPrefab( category.AudioObjectPrefab );
                }
            }
        }

        private List<AudioController> _additionalAudioControllers;

        private void _RegisterAdditionalAudioController( AudioController ac )
        {
            if( _additionalAudioControllers == null )
            {
                _additionalAudioControllers = new List<AudioController>();
            }

            _additionalAudioControllers.Add( ac );

            _InvalidateCategories();
            _SyncCategoryVolumes( ac, this );
        }

        private void _SyncCategoryVolumes( AudioController toSync, AudioController syncWith )
        {
            for( int i = 0; i < toSync.AudioCategories.Length; i++ )
            {
                var catDest = toSync.AudioCategories[i];
                var catSource = syncWith._GetCategory( catDest.Name );
                if( catSource != null )
                {
                    catDest.Volume = catSource.Volume;
                }
            }
        }

        private void _UnregisterAdditionalAudioController( AudioController ac )
        {
            if( _additionalAudioControllers != null )
            {
                int i;
                for( i = 0; i < _additionalAudioControllers.Count; i++ )
                {
                    if( _additionalAudioControllers[i] == ac )
                    {
                        _additionalAudioControllers.RemoveAt( i );
                        _InvalidateCategories();
                        return;
                    }
                }

            }
            else
            {
                Debug.LogWarning( "_UnregisterAdditionalAudioController: AudioController " + ac.name + " not found" );
            }

        }

        private static List<AudioCategory> _GetAllCategories( string name )
        {
            var primaryInstance = Instance;

            var catList = new List<AudioCategory>();

            AudioCategory cat = primaryInstance._GetCategory( name );

            if( cat != null )
            {
                catList.Add( cat );
            }

            if( primaryInstance._additionalAudioControllers != null )
            {
                for( int index = 0; index < primaryInstance._additionalAudioControllers.Count; index++ )
                {
                    var ac = primaryInstance._additionalAudioControllers[index];
                    cat = ac._GetCategory( name );

                    if( cat != null )
                    {
                        catList.Add( cat );
                    }
                }
            }
            // also get categories of playing audios because their AudioController might be already destroyed (e.g. due to level change) but we still need to adjust a categroy
            InvokeForAllPlayingAudioObjects( ( o ) =>
            {
                if( o.DoesBelongToCategory( name, out var catOut ) )
                {
                    if( !catList.Contains( catOut) ) catList.Add( catOut ); 
                }
            }, true );
            return catList;
        }

        /// <summary>
        /// Plays a specific AudioSubItem.
        /// </summary>
        /// <remarks>
        /// This function is used by the editor extension and is normally not required for application developers. 
        /// Use <see cref="AudioController.Play(string)"/> instead.
        /// </remarks>
        /// <param name="subItem">the <see cref="AudioSubItem"/></param>
        /// <param name="volume">the volume</param>
        /// <param name="worldPosition">the world position </param>
        /// <param name="parentObj">the parent object, or <c>null</c></param>
        /// <param name="delay">the delay in seconds</param>
        /// <param name="startTime">the start time seconds</param>
        /// <param name="playWithoutAudioObject">if <c>true</c>plays the audio by using the Unity 
        /// function <c>PlayOneShot</c> without creating an audio game object. Allows playing audios from within the Unity inspector.
        /// </param>
        /// <param name="useExistingAudioObj">if specified this existing audio object is used instead of creating a new <see cref="AudioObject"/></param>
        /// <param name="dspTime">The high precision DSP time at which to schedule playing the audio [default=0]</param>
        /// <param name="channel">if <c>true</c>specifies the audio channel</param>
        /// <param name="startVolumeMultiplier">allows to adjust the start volume if e.g. a FadeOut will follow immediately after</param>
        /// <returns>
        /// The created <see cref="AudioObject"/> or <c>null</c>
        /// </returns>
        public AudioObject PlayAudioSubItem( AudioSubItem subItem, float volume, Vector3 worldPosition, Transform parentObj, double delay, double startTime, bool playWithoutAudioObject, AudioObject useExistingAudioObj, double dspTime = 0, AudioChannelType channel = AudioChannelType.Default, float startVolumeMultiplier = 1 )
        {
            _ValidateCategories();

            var audioItem = subItem.item;

            switch( subItem.SubItemType )
            {
            case AudioSubItemType.Item:
                if( subItem.ItemModeAudioID.Length == 0 )
                {
                    Debug.LogWarning( "No item specified in audio sub-item with ITEM mode (audio item: '" + audioItem.Name + "')" );
                    return null;
                }
                return _PlayInChannel( subItem.ItemModeAudioID, channel, volume, worldPosition, parentObj, delay, startTime, playWithoutAudioObject, dspTime, useExistingAudioObj );

            case AudioSubItemType.Clip:
                break;
            }

            if( subItem.Clip == null )
            {
                return null;
            }

            //Debug.Log( "PlayAudioSubItem Clip '" + subItem.Clip.name + "'" );

            var audioCategory = audioItem.category;

            float volumeWithoutCategory = subItem.Volume * audioItem.Volume * volume;

            if( subItem.RandomVolume != 0 || audioItem.loopSequenceRandomVolume != 0 )
            {
                float randomVolume = subItem.RandomVolume + audioItem.loopSequenceRandomVolume;
                volumeWithoutCategory += RandomHelper.Range( -randomVolume, randomVolume );
                volumeWithoutCategory = Mathf.Clamp01( volumeWithoutCategory );
            }

            float volumeWithCategory = volumeWithoutCategory * audioCategory.VolumeTotal;

            var subItemAudioController = _GetAudioController( subItem );

            if( !subItemAudioController.PlayWithZeroVolume && ( volumeWithCategory <= 0 || Volume <= 0 ) )
            {
                return null;
            }

            GameObject audioObjInstance;

            //Debug.Log( "PlayAudioItem clip:" + subItem.Clip.name );

            GameObject audioPrefab = audioCategory.GetAudioObjectPrefab();

            if( audioPrefab == null )
            {
                if( subItemAudioController.AudioObjectPrefab != null )
                {
                    audioPrefab = subItemAudioController.AudioObjectPrefab;
                }
                else
                    audioPrefab = AudioObjectPrefab;
            }

            if( playWithoutAudioObject )
            {
                audioPrefab.GetComponent<AudioSource>().PlayOneShot( subItem.Clip, AudioObject.TransformVolume( volumeWithCategory ) ); // unfortunately produces warning message, but works (tested only with Unity 3.5)

                //AudioSource.PlayClipAtPoint( subItem.Clip, Vector3.zero, AudioObject.TransformVolume( volumeWithCategory ) );
                return null;
            }

            AudioObject sndObj;

            if( useExistingAudioObj == null )
            {
#if AUDIO_TOOLKIT_DEMO
            audioObjInstance = (GameObject) GameObject.Instantiate( audioPrefab, worldPosition, Quaternion.identity );

#else
                if( subItemAudioController.UsePooledAudioObjects )
                {
                    audioObjInstance = (GameObject)ObjectPoolController.Instantiate( audioPrefab, worldPosition, Quaternion.identity );
                }
                else
                {
                    audioObjInstance = (GameObject)ObjectPoolController.InstantiateWithoutPool( audioPrefab, worldPosition, Quaternion.identity );
                }
#endif
                if( parentObj )
                {
                    audioObjInstance.transform.parent = parentObj;
                }
                else
                {
                    if( !audioItem.DestroyOnLoad )
                    {
                        // Only makes sense on root objects. If the audio is parented it will get destroyed if the parent object is destroyed during scene load
                        if( audioObjInstance.transform.parent == null )
                        {
                            DontDestroyOnLoad( audioObjInstance );
                        }
                        else
                        {
                            Debug.LogWarning( "Audio Item '" + audioItem.Name + "' should survive scene changes but is parented to another object. It will get destroyed if the parent object gets destroyed during scene change" );
                        }
                    }
                }

                sndObj = audioObjInstance.gameObject.GetComponent<AudioObject>();
            }
            else
            {
                audioObjInstance = useExistingAudioObj.gameObject;
                sndObj = useExistingAudioObj;
            }

            sndObj.subItem = subItem;

            if( object.ReferenceEquals( useExistingAudioObj, null ) )
            {
                sndObj._lastChosenSubItemIndex = audioItem._lastChosen;
            }

            sndObj.primaryAudioSource.clip = subItem.Clip;
            audioObjInstance.name = "AudioObject:" + sndObj.primaryAudioSource.clip.name;

            sndObj.primaryAudioSource.pitch = AudioObject.TransformPitch( subItem.PitchShift );
#if UNITY_5_OR_NEWER
            sndObj.primaryAudioSource.panStereo = subItem.Pan2D;
            //sndObj.primaryAudioSource.spatialBlend = audioItem.spatialBlend;
#else
        sndObj.primaryAudioSource.pan = subItem.Pan2D;
#endif

            if( subItem.RandomStartPosition )
            {
                startTime = RandomHelper.Range( 0, (float)sndObj.clipLength );
            }

            sndObj.pcmTime = (startTime + subItem.ClipStartTime);

            sndObj.primaryAudioSource.loop = ( audioItem.Loop == AudioItem.LoopMode.LoopSubitem || audioItem.Loop == (AudioItem.LoopMode)3 ); // 3... deprecated gapless loop mode

            sndObj._volumeExcludingCategory = volumeWithoutCategory;
            sndObj._volumeFromScriptCall = volume;
            sndObj.category = audioCategory;
            sndObj.channel = channel;

            if( subItem.FadeIn > 0 )
            {
                // call FadeIn to correctly set start volume in _ApplyVolumePrimary
                sndObj.FadeIn( subItem.FadeIn );
            }

            sndObj._ApplyVolumePrimary( startVolumeMultiplier );

#if UNITY_5_OR_NEWER

            var audioMixerGroup = audioCategory.GetAudioMixerGroup();
            if( audioMixerGroup )
            {
                sndObj.primaryAudioSource.outputAudioMixerGroup = audioCategory.audioMixerGroup;
            }
#endif

            if( subItem.RandomPitch != 0 || audioItem.loopSequenceRandomPitch != 0 )
            {
                float randomPitch = subItem.RandomPitch + audioItem.loopSequenceRandomPitch;
                sndObj.primaryAudioSource.pitch *= AudioObject.TransformPitch( RandomHelper.Range( -randomPitch, randomPitch ) );
            }

            if( subItem.RandomDelay > 0 )
            {
                delay += RandomHelper.Range( 0, subItem.RandomDelay );
            }

            if( dspTime > 0 )
            {
                sndObj.PlayScheduled( dspTime + delay + subItem.Delay + audioItem.Delay );
            }
            else
                sndObj.Play( (float)delay + subItem.Delay + audioItem.Delay );

            if( subItem.FadeIn > 0 )
            {
                sndObj.FadeIn( subItem.FadeIn );
            }

#if UNITY_EDITOR && !AUDIO_TOOLKIT_DEMO
            var logData = new AudioLog.LogData_PlayClip();
            logData.audioID = audioItem.Name;
            logData.category = audioCategory.Name;
            logData.clipName = subItem.Clip.name;
            logData.delay = (float)delay;
            logData.parentObjectName = parentObj != null ? parentObj.name : "";
            logData.parentObject = parentObj != null ? parentObj.gameObject : null;
            logData.position = worldPosition;
            logData.startTime = (float)startTime;
            logData.volume = volumeWithCategory;
            logData.pitch = sndObj.primaryAudioSource.pitch;

#if UNITY_AUDIO_FEATURES_4_1
            if( dspTime > 0 )
            {
                logData.scheduledDspTime = Time.time + (float)( dspTime - AudioSettings.dspTime );
            }
#endif

            AudioLog.Log( logData );
#endif

            return sndObj;
        }

        private AudioController _GetAudioController( AudioSubItem subItem )
        {
            if( subItem.item != null && subItem.item.category != null )
            {
                return subItem.item.category.audioController;
            }
            return this;
        }

        internal void _NotifyPlaylistTrackCompletelyPlayed( AudioObject audioObject )
        {
            audioObject._isCurrentPlaylistTrack = false;
            var ch = AudioController._GetAudioChannelInternal( audioObject.channel );
            if( ch.IsPlaylistPlaying() )
            {
                if( ch.currentlyPlaying == audioObject )
                {
                    if( _PlayNextTrackOnPlaylist( audioObject.channel, ch.settings.delayBetweenPlaylistTracks ) == null )
                    {
                        ch.isPlaylistPlaying = false;
                        if( playlistFinishedEvent != null )
                        {
                            playlistFinishedEvent( audioObject.channel, ch.currentPlaylist );
                        }
                    }
                }
            }
        }

        private void _ValidateAudioObjectPrefab( GameObject audioPrefab )
        {
            if( UsePooledAudioObjects )
            {
#if AUDIO_TOOLKIT_DEMO
        Debug.LogWarning( "Poolable Audio objects not supported by the Audio Toolkit Demo version" );
#else
                if( audioPrefab.GetComponent<PoolableObject>() == null )
                {
                    Debug.LogWarning( "AudioObject prefab does not have the PoolableObject component. Pooling will not work." );
                }
                else
                {
                    ObjectPoolController.Preload( audioPrefab );
                }
#endif
            }

            if( audioPrefab.GetComponent<AudioObject>() == null )
            {
                Debug.LogError( "AudioObject prefab must have the AudioObject script component!" );
            }
        }

        // is public because custom inspector must access it
        public AudioController_CurrentInspectorSelection _currentInspectorSelection = new AudioController_CurrentInspectorSelection();

#pragma warning disable 612

        public void OnAfterDeserialize()
        {
            if( audioChannels == null || audioChannels.Length == 0 )
            {
                audioChannels = new AudioChannelWithInternalInfos[AudioChannel.AUDIO_CHANNEL_COUNT];
                for( int i = 0; i < audioChannels.Length; i++ )
                {
                    audioChannels[i] = new AudioChannelWithInternalInfos();
                }
            }
            bool shouldUpgradeToV11 = _musicCrossFadeTime_Out >= 0;

            if( shouldUpgradeToV11 )
            {
                Debug.Log( "Upgrading AudioController from older than v11" );
                var musicChannel = audioChannels[(int)AudioChannelType.Music];
                var ambiChannel = audioChannels[(int)AudioChannelType.Ambience];
                if( specifyCrossFadeInAndOutSeperately )
                {
                    musicChannel.settings.crossfadeTime_In = _musicCrossFadeTime_In;
                    musicChannel.settings.crossfadeTime_Out = _musicCrossFadeTime_Out;
                    ambiChannel.settings.crossfadeTime_In = _ambienceSoundCrossFadeTime_In;
                    ambiChannel.settings.crossfadeTime_Out = _ambienceSoundCrossFadeTime_Out;
                }
                else
                {
                    musicChannel.settings.crossfadeTime_In = musicCrossFadeTime;
                    musicChannel.settings.crossfadeTime_Out = musicCrossFadeTime;
                    ambiChannel.settings.crossfadeTime_In = ambienceSoundCrossFadeTime;
                    ambiChannel.settings.crossfadeTime_Out = ambienceSoundCrossFadeTime;
                }

                musicChannel.settings.loopPlaylist = loopPlaylist;
                musicChannel.settings.shufflePlaylist = shufflePlaylist;
                musicChannel.settings.crossfadePlaylist = crossfadePlaylist;
                musicChannel.settings.delayBetweenPlaylistTracks = delayBetweenPlaylistTracks;

                ambiChannel.settings.loopPlaylist = loopPlaylist;
                ambiChannel.settings.shufflePlaylist = shufflePlaylist;
                ambiChannel.settings.crossfadePlaylist = crossfadePlaylist;
                ambiChannel.settings.delayBetweenPlaylistTracks = delayBetweenPlaylistTracks;

                _musicCrossFadeTime_Out = -999;
            }

        }
#pragma warning restore 612

        public void OnBeforeSerialize()
        {

        }
    }

    [Serializable]
    public class AudioController_CurrentInspectorSelection
    {
        public int currentCategoryIndex = 0;
        public int currentItemIndex = 0;
        public int currentSubitemIndex = 0;
        public int currentPlaylistEntryIndex = 0;
        public int currentPlaylistIndex = 0;
    }
}

