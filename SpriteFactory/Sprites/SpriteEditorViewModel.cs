﻿using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using Catel.Collections;
using Catel.IoC;
using Catel.MVVM;
using Catel.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using SpriteFactory.MonoGameControls;

namespace SpriteFactory.Sprites
{
    public class SpriteEditorViewModel : MonoGameViewModel
    {
        private SpriteBatch _spriteBatch;
        private Texture2D _backgroundTexture;
        private SpriteFont _spriteFont;

        public SpriteEditorViewModel()
        {
            SelectTextureCommand = new Command(SelectTexture);

            AddAnimationCommand = new Command(AddAnimation);
            RemoveAnimationCommand = new Command(RemoveAnimation, () => SelectedAnimation != null);

            SelectedPreviewZoom = PreviewZoomOptions.LastOrDefault();
        }

        public override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);

            _backgroundTexture = Content.Load<Texture2D>("checkered-dark");
            _spriteFont = Content.Load<SpriteFont>("default");

            Camera = new OrthographicCamera(GraphicsDevice);
            Camera.LookAt(Vector2.Zero);
        }

        public int Width => GraphicsDevice.Viewport.Width;
        public int Height => GraphicsDevice.Viewport.Height;

        public OrthographicCamera Camera { get; private set; }

        private Cursor _cursor;
        public Cursor Cursor
        {
            get => _cursor;
            set => SetPropertyValue(ref _cursor, value, nameof(Cursor));
        }

        public ZoomOptionViewModel[] PreviewZoomOptions { get; } =
        {
            new ZoomOptionViewModel(1),
            new ZoomOptionViewModel(2),
            new ZoomOptionViewModel(4),
            new ZoomOptionViewModel(8)
        };

        private ZoomOptionViewModel _selectedPreviewZoom;
        public ZoomOptionViewModel SelectedPreviewZoom
        {
            get => _selectedPreviewZoom;
            set => SetPropertyValue(ref _selectedPreviewZoom, value, nameof(SelectedPreviewZoom));
        }

        public Vector2 Origin => Texture != null ? new Vector2(Texture.Width / 2f, Texture.Height / 2f) : Vector2.Zero;
        public Rectangle TextureBounds => Texture?.Bounds ?? Rectangle.Empty;

        private string _texturePath;
        public string TexturePath
        {
            get => _texturePath;
            private set
            {
                if (SetPropertyValue(ref _texturePath, value, nameof(TexturePath)))
                {
                    TextureName = Path.GetFileName(_texturePath);
                    Texture = _texturePath != null ? Content.LoadRaw<Texture2D>(_texturePath) : null;
                }
            }
        }
        
        private string _textureName;
        public string TextureName
        {
            get => _textureName ?? "(no texture selected)";
            private set => SetPropertyValue(ref _textureName, value, nameof(TextureName));
        }

        private Texture2D _texture;
        public Texture2D Texture
        {
            get => _texture;
            private set
            {
                if (SetPropertyValue(ref _texture, value, nameof(Texture)))
                {
                    if(_texture != null)
                        Camera.LookAt(_texture.Bounds.Center.ToVector2());
                }
            }
        }
        
        public ICommand SelectTextureCommand { get; }

        private int _tileWidth = 32;
        public int TileWidth
        {
            get => _tileWidth;
            set => SetPropertyValue(ref _tileWidth, value, nameof(TileWidth));
        }

        private int _tileHeight = 32;
        public int TileHeight
        {
            get => _tileHeight;
            set => SetPropertyValue(ref _tileHeight, value, nameof(TileHeight));
        }

        public Vector2 WorldPosition { get; set; }

        public ObservableCollection<SpriteKeyFrameAnimation> Animations { get; } = new ObservableCollection<SpriteKeyFrameAnimation>();

        private SpriteKeyFrameAnimation _selectedAnimation;
        public SpriteKeyFrameAnimation SelectedAnimation
        {
            get => _selectedAnimation;
            set => SetPropertyValue(ref _selectedAnimation, value, nameof(SelectedAnimation));
        }

        public ICommand AddAnimationCommand { get; }
        public ICommand RemoveAnimationCommand { get; }

        private void AddAnimation()
        {
            var animation = new SpriteKeyFrameAnimation {Name = $"animation{Animations.Count}"};
            Animations.Add(animation);
            SelectedAnimation = animation;
        }

        private void RemoveAnimation()
        {
            if (SelectedAnimation != null)
            {
                var index = Animations.IndexOf(SelectedAnimation);
                Animations.Remove(SelectedAnimation);
                SelectedAnimation = index >= Animations.Count ? Animations.LastOrDefault() : Animations[index];
            }
        }

        private async void SelectTexture()
        {
            var openFileService = DependencyResolver.Resolve<IOpenFileService>();
            openFileService.Filter = "PNG Files (*.png)|*.png|All Files (*.*)|*.*";

            if (await openFileService.DetermineFileAsync())
                TexturePath = openFileService.FileName;
        }

        //private void AutoDetect()
        //{
        //    var data = new Color[Texture.Width * Texture.Height];
        //    Texture.GetData(data);

        //    for (var y = 0; y < Texture.Height; y++)
        //    {
        //        for (var x = 0; x < Texture.Width; x++)
        //        {
        //            var color = data[y * Texture.Height + x];

        //            if (color.A != 0)
        //            {
        //                if(_autoRectangle.IsEmpty)
        //                    _autoRectangle = new Rectangle(x, y, 1, 1);
        //            }
        //        }
        //    }
        //}

        private Vector2 _previousMousePosition;
        
        public override void OnMouseDown(MouseStateArgs mouseState)
        {
            if (mouseState.LeftButton == ButtonState.Pressed)
            {
                AddAnimation();
                var frameIndex = GetFrameIndex();

                if (frameIndex.HasValue)
                    SelectedAnimation?.KeyFrames.Add(frameIndex.Value);
            }
        }

        public override void OnMouseWheel(MouseStateArgs args, int delta)
        {
            Camera.ZoomIn(delta / 1000f);
        }

        private int? GetFrameIndex()
        {
            if (Texture == null || !Texture.Bounds.Contains(WorldPosition))
                return null;

            var columns = Texture.Width / TileWidth;
            var cx = (int)(WorldPosition.X / TileWidth);
            var cy = (int)(WorldPosition.Y / TileHeight);
            var frameIndex = cy * columns + cx;

            return frameIndex;
        }

        private Rectangle GetFrameRectangle(int frame)
        {
            var columns = Texture.Width / TileWidth;
            var cy = frame / columns;
            var cx = frame - cy * columns;
            return new Rectangle(cx * TileWidth, cy * TileHeight, TileWidth, TileHeight);
        }

        public override void OnMouseMove(MouseStateArgs mouseState)
        {
            WorldPosition = Camera.ScreenToWorld(mouseState.Position);
            
            var previousWorldPosition = Camera.ScreenToWorld(_previousMousePosition);
            var mouseDelta = previousWorldPosition - WorldPosition;

            if (mouseState.RightButton == ButtonState.Pressed)
                Camera.Move(mouseDelta);

            if (mouseState.LeftButton == ButtonState.Pressed && SelectedAnimation != null)
            {
                var frameIndex = GetFrameIndex();

                if (frameIndex.HasValue && !SelectedAnimation.KeyFrames.Contains(frameIndex.Value))
                    SelectedAnimation?.KeyFrames.Add(frameIndex.Value);
            }

            _previousMousePosition = mouseState.Position;
        }

        private int _frameIndex;
        private int _nextFrameHackCounter;


        private Rectangle GetPreviewRectangle()
        {
            if(TileWidth == 0 || TileHeight == 0)
                return Rectangle.Empty;

            const int max = 256;
            var previewZoom = SelectedPreviewZoom.Value;
            var width = TileWidth * previewZoom;
            var height = TileHeight * previewZoom;
            var ratio = TileWidth / (float) TileHeight;

            if (width > max || height > max)
            {
                if (ratio >= 1f)
                {
                    width = max;
                    height = (int) (max / ratio);
                }
                else if (height > max)
                {
                    height = max;
                    width = (int) (max * ratio);
                }
            }

            var x = GraphicsDevice.Viewport.Width - width;
            return new Rectangle(x, 0, width, height);
        }

        public SpritesFile SaveDocument(string filePath)
        {
            return new SpritesFile
            {
                Texture = Catel.IO.Path.GetRelativePath(TexturePath, Path.GetDirectoryName(filePath)),
                Mode = SpriteMode.Tileset,
                Content = new TilesetContent
                {
                    TileWidth = TileWidth,
                    TileHeight = TileHeight
                },
                Animations = Animations.ToList()
            };
        }

        public void LoadDocument(string filePath, SpritesFile data)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                TexturePath = null;
            }
            else
            {
                var directory = Path.GetDirectoryName(filePath);
                // ReSharper disable once AssignNullToNotNullAttribute
                TexturePath = Path.Combine(directory, data.Texture);
            }

            TileWidth = data.Content.TileWidth;
            TileHeight = data.Content.TileHeight;
            Animations.Clear();
            Animations.AddRange(data.Animations);
            SelectedAnimation = Animations.FirstOrDefault();
        }


        public override void Update(GameTime gameTime)
        {
        }

        public override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);

            if (Texture == null)
                return;

            // main texture
            var boundingRectangle = TextureBounds;

            _spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointWrap, transformMatrix: Camera.GetViewMatrix());
            _spriteBatch.Draw(_backgroundTexture, sourceRectangle: boundingRectangle, destinationRectangle: boundingRectangle, color: Color.White);

            if (SelectedAnimation != null)
            {
                foreach (var keyFrame in SelectedAnimation.KeyFrames)
                {
                    var keyFrameRectangle = GetFrameRectangle(keyFrame);
                    _spriteBatch.FillRectangle(keyFrameRectangle, Color.CornflowerBlue * 0.5f);
                }
            }

            _spriteBatch.Draw(Texture, sourceRectangle: boundingRectangle, destinationRectangle: boundingRectangle, color: Color.White);

            // highlighter
            if (TileWidth > 1 && TileHeight > 1)
            {
                for (var y = 0; y <= Texture.Height; y += TileHeight)
                    _spriteBatch.DrawLine(0, y, boundingRectangle.Width, y, Color.White * 0.5f);

                for (var x = 0; x <= Texture.Width; x += TileWidth)
                    _spriteBatch.DrawLine(x, 0, x, boundingRectangle.Height, Color.White * 0.5f);

                if (boundingRectangle.Contains(WorldPosition))
                {
                    var cx = (int)(WorldPosition.X / TileWidth);
                    var cy = (int)(WorldPosition.Y / TileHeight);

                    _spriteBatch.FillRectangle(cx * TileWidth, cy * TileHeight, TileWidth, TileHeight, Color.CornflowerBlue * 0.5f);
                }
            }

            _spriteBatch.End();

            // animation preview
            if (SelectedAnimation != null && SelectedAnimation.KeyFrames.Any())
            {
                _nextFrameHackCounter++;

                if (_nextFrameHackCounter >= 10)
                {
                    _frameIndex++;
                    _nextFrameHackCounter = 0;
                }

                if (_frameIndex >= SelectedAnimation.KeyFrames.Count)
                    _frameIndex = 0;

                var frame = SelectedAnimation.KeyFrames[_frameIndex];
                var sourceRectangle = GetFrameRectangle(frame);
                var previewRectangle = GetPreviewRectangle();

                _spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointWrap);
                _spriteBatch.Draw(_backgroundTexture, previewRectangle, null, Color.White);
                _spriteBatch.DrawRectangle(previewRectangle, Color.White * 0.5f);
                _spriteBatch.Draw(Texture, previewRectangle, sourceRectangle, Color.White);
                _spriteBatch.End();
            }

            // debug text
            var frameIndex = GetFrameIndex();
            var frameRectangle = frameIndex.HasValue ? GetFrameRectangle(frameIndex.Value) : Rectangle.Empty;
            _spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointWrap);
            _spriteBatch.DrawString(_spriteFont, $"{frameIndex}: {frameRectangle}", Vector2.Zero, Color.White);
            _spriteBatch.End();
        }
    }
}
