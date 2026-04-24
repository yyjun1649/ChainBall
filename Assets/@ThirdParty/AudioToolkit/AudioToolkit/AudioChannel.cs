using System;
using System.Collections.Generic;
using CS.Essentials;
using UnityEngine;

namespace CS.AudioToolkit
{
    public enum AudioChannelType
    {
        Default = 0,
        Music = 1,
        Ambience = 2,
        Custom1 = 3,
        Custom2 = 4,
        Custom3 = 5,
        Custom4 = 6,
    }

    [Serializable]
    public class AudioChannelSettings
    {
        /// <summary>
        /// If set to a value > 0 (in seconds) the channel will automatically be cross-faded with this fading-in time.
        /// </summary>
        public float crossfadeTime_In = 0;

        /// <summary>
        /// If set to a value > 0 (in seconds) the channel will automatically be cross-faded with this fading-out time.
        /// </summary>
        public float crossfadeTime_Out = 0;

        /// <summary>
        /// specifies if the music playlist will get looped
        /// </summary>
        public bool loopPlaylist = false;
        
        /// <summary>
        /// enables / disables shuffling for the music playlist
        /// </summary>
        public bool shufflePlaylist = false;

        /// <summary>
        /// if enabled, the tracks on the playlist will get cross-faded as specified by <see cref="musicCrossFadeTime"/>
        /// </summary>
        public bool crossfadePlaylist = false;

        /// <summary>
        /// Mute time in between two tracks on the playlist.
        /// </summary>
        public float delayBetweenPlaylistTracks = 1;

    }

    [Serializable]
    public class AudioChannel
    {
        public const int AUDIO_CHANNEL_COUNT = 7;
        public AudioChannelSettings settings = new AudioChannelSettings();

#if AUDIO_TOOLKIT_DEMO
        public AudioObject currentlyPlaying;
#else
        public PoolableReference<AudioObject> _currentlyPlaying;
        public AudioObject currentlyPlaying
        {
            set
            {
                if( _currentlyPlaying == null )
                {
                    _currentlyPlaying = new PoolableReference<AudioObject>();
                }
                _currentlyPlaying.Set( value, true );
            }
            get
            {
                if( _currentlyPlaying == null ) return null;
                return _currentlyPlaying.Get();
            }
        }

        private void ValidatePoolableReference()
        {
            
        }
#endif
        private bool _enabled = true;
        public bool enabled
        {
            get { return _enabled; }
            set
            {
                if( _enabled == value ) return;
                _enabled = value;

                if( currentlyPlaying )
                {
                    if( value )
                    {
                        if( currentlyPlaying.IsPaused() )
                        {
                            currentlyPlaying.Play();
                        }
                    }
                    else
                    {
                        currentlyPlaying.Pause();
                    }
                }
            }
        }

        /// <summary>
        /// Stops the currently playing channel track with fade-out.
        /// </summary>
        /// <param name="fadeOut">The fade-out time in seconds.</param>
        /// <returns>
        /// <c>true</c> if channel was stopped, otherwise <c>false</c>
        /// </returns>
        public bool Stop( float fadeOutLength = 0 )
        {
            if( currentlyPlaying != null )
            {
                currentlyPlaying.Stop( fadeOutLength );
                currentlyPlaying = null;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Pauses the currently playing channel track.
        /// </summary>
        /// <param name="fadeOut">The fade-out time in seconds.</param>
        /// <returns>
        /// <c>true</c> if channel was paused, otherwise <c>false</c>
        /// </returns>
        public bool Pause( float fadeOutLength = 0 )
        {
            if( currentlyPlaying != null )
            {
                currentlyPlaying.Pause( fadeOutLength );
                return true;
            }
            return false;
        }

        /// <summary>
        /// Unpauses the currently playing channel track.
        /// </summary>
        /// <param name="fadeIn">The fade-in time in seconds.</param>
        /// <returns>
        /// <c>true</c> if channel was unpaused, otherwise <c>false</c>
        /// </returns>
        public void Unpause( float fadeIn = 0 )
        {
            if( currentlyPlaying != null && enabled )
            {
                currentlyPlaying.Unpause( fadeIn );
            }
        }
        /// <summary>
        /// Used to check if channel audio is paused
        /// </summary>
        /// <returns>
        /// <c>true</c> if audio is paused, otherwise <c>false</c>
        /// </returns>
        public bool isPaused()
        {
            if( currentlyPlaying != null )
            {
                return currentlyPlaying.IsPaused();
            }
            return false;
        }

        /// <summary>
        /// Uses to check if channel audio is actually playing
        /// </summary>
        /// <returns>
        /// <c>true</c> if audio is playing, otherwise <c>false</c>
        /// </returns>
        public bool isPlaying()
        {
            if( currentlyPlaying != null )
            {
                return currentlyPlaying.IsPlaying();
            }
            return false;
        }        

        /// <summary>
        /// Determines whether the playlist is playing.
        /// </summary>
        /// <returns>
        ///   <c>true</c> if the current track is from the playlist; otherwise, <c>false</c>.
        /// </returns>
        virtual public bool IsPlaylistPlaying()
        {
            return false;
        }

        /// <summary>
        /// sets/gets the default parent transform for audio in this channel. 
        /// Will be used in all AudioController.Play(...) methods that don't take a seperate parent Transform.
        /// </summary>
        /// <remarks>Changing the transform does not effect the currently playing ambience sound</remarks>
        public Transform parent { get; set; }
    }

    [Serializable]
    internal class AudioChannelWithInternalInfos : AudioChannel
    {
        [System.NonSerialized]
        internal List<int> playlistPlayed = new List<int>();
        [System.NonSerialized]
        internal bool isPlaylistPlaying;
        [System.NonSerialized]
        internal Playlist currentPlaylist;

        internal void SetCurrentPlaylist( Playlist playlist )
        {
            currentPlaylist = playlist;
            ClearPlaylistPlayed();
        }

        public override bool IsPlaylistPlaying()
        {
            if( isPlaylistPlaying )
            {
                if( !currentlyPlaying )
                {
                    isPlaylistPlaying = false;
                    return false;
                }
                return true;
            }
            return false;
        }

        internal int _GetNextTrack()
        {
            Playlist currentPl = currentPlaylist;
            if( currentPl == null || currentPl.playlistItems == null )
            {
                Debug.LogWarning( "There is no current playlist set" );
                return -1;
            }
            if( currentPl.playlistItems.Length == 1 ) return 0;

            if( settings.shufflePlaylist )
            {
                return _GetNextTrackShuffled();
            }
            else
            {
                return _GetNextTrackInOrder();

            }
        }

        internal int _GetPreviousTrack()
        {
            Playlist currentPl = currentPlaylist;

            if( currentPl.playlistItems.Length == 1 ) return 0;

            if( settings.shufflePlaylist )
            {
                return _GetPreviousTrackShuffled();
            }
            else
            {
                return _GetPreviousTrackInOrder();

            }
        }

        private int _GetPreviousTrackShuffled()
        {
            if( playlistPlayed.Count >= 2 )
            {
                int id = playlistPlayed[playlistPlayed.Count - 2];

                _RemoveLastPlayedOnList();
                _RemoveLastPlayedOnList();

                return id;
            }
            else
                return -1;
        }

        private void _RemoveLastPlayedOnList()
        {
            playlistPlayed.RemoveAt( playlistPlayed.Count - 1 );
        }

        private int _GetNextTrackShuffled()
        {
            var playedTracksHashed = new HashSet<int>();

            int disallowTracksCount = playlistPlayed.Count;

            int randomElementCount;

            Playlist currentPl = currentPlaylist;

            if( currentPl.playlistItems.Length == 0 ) return -1;

            if( settings.loopPlaylist )
            {
                randomElementCount = Mathf.Clamp( currentPl.playlistItems.Length / 4, 2, 10 );

                if( disallowTracksCount > currentPl.playlistItems.Length - randomElementCount )
                {
                    disallowTracksCount = currentPl.playlistItems.Length - randomElementCount;

                    if( disallowTracksCount < 1 ) // the same track must never be played twice in a row
                    {
                        disallowTracksCount = 1; // musicPlaylist.Length is always >= 2 
                    }
                }
            }
            else
            {
                // do not play the same song twice
                if( disallowTracksCount >= currentPl.playlistItems.Length )
                {
                    return -1; // stop playing as soon as all tracks have been played 
                }
            }

            for( int i = 0; i < disallowTracksCount; i++ )
            {
                playedTracksHashed.Add( playlistPlayed[playlistPlayed.Count - 1 - i] );
            }

            var possibleTrackIDs = new List<int>();

            for( int i = 0; i < currentPl.playlistItems.Length; i++ )
            {
                if( !playedTracksHashed.Contains( i ) )
                {
                    possibleTrackIDs.Add( i );
                }
            }

            return possibleTrackIDs[UnityEngine.Random.Range( 0, possibleTrackIDs.Count )];
        }

        private int _GetNextTrackInOrder()
        {
            if( playlistPlayed.Count == 0 )
            {
                return 0;
            }
            int next = playlistPlayed[playlistPlayed.Count - 1] + 1;

            Playlist currentPl = currentPlaylist;

            if( next >= currentPl.playlistItems.Length ) // reached the end of the playlist
            {
                if( settings.loopPlaylist )
                {
                    next = 0;
                }
                else
                    return -1;
            }
            return next;
        }

        private int _GetPreviousTrackInOrder()
        {
            Playlist currentPl = currentPlaylist;

            if( playlistPlayed.Count < 2 )
            {
                if( settings.loopPlaylist )
                {
                    return currentPl.playlistItems.Length - 1;
                }
                else
                    return -1;
            }

            int next = playlistPlayed[playlistPlayed.Count - 1] - 1;

            _RemoveLastPlayedOnList();
            _RemoveLastPlayedOnList();

            if( next < 0 ) // reached the end of the playlist
            {
                if( settings.loopPlaylist )
                {
                    next = currentPl.playlistItems.Length - 1;
                }
                else
                    return -1;
            }
            return next;
        }

        internal void ClearPlaylistPlayed()
        {
            playlistPlayed.Clear();
        }
    }
}