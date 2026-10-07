using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoGame.Extended.Tilemaps.Rendering;

/// <summary>
/// Tilemap renderer using SpriteBatch for simple integration with existing SpriteBatch-based code.
/// </summary>
/// <remarks>
/// <para>
/// This renderer draws tiles by issuing one SpriteBatch.Draw call per visible tile. Frustum
/// culling is applied per layer so only tiles within the camera's view are submitted, keeping
/// the per-frame draw call count proportional to the number of visible tiles rather than the
/// total tile count.
/// </para>
/// <para>
/// Consecutive visible tile layers that share the same parallax factor are batched into a single
/// SpriteBatch.Begin/End pair to minimize state-change overhead. Layers with different parallax
/// factors each require a separate Begin/End pair.
/// </para>
/// </remarks>
public sealed class TilemapSpriteBatchRenderer
{
    private Tilemap _tilemap;
    private readonly List<TilemapTileData> _animatedTiles = new List<TilemapTileData>();
    private TileRenderPadding _tileRenderPadding;

    /// <summary>
    /// Gets or sets the blend state used when drawing tiles.
    /// </summary>
    /// <remarks>
    /// The default is <see cref="BlendState.NonPremultiplied"/>, which is correct for tileset
    /// textures loaded directly from file where the PNG pixel data has not been premultiplied.
    /// Set this to <see cref="BlendState.AlphaBlend"/> if your textures are loaded through
    /// the MonoGame content pipeline, which premultiplies alpha by default.
    /// </remarks>
    public BlendState BlendState { get; set; } = BlendState.NonPremultiplied;

    /// <summary>
    /// Gets or sets the sampler state used when drawing tiles.
    /// </summary>
    /// <remarks>
    /// The default is <see cref="SamplerState.PointClamp"/>, which is correct for pixel-art
    /// tilesets. It produces clean, hard-edged tiles at any zoom level or rotation angle
    /// with no edge bleeding between adjacent tiles in the texture atlas.
    /// <see cref="SamplerState.LinearClamp"/> enables smooth sub-pixel interpolation for
    /// non-pixel-art tilesets, but produces fringe artifacts at tile edges when the camera
    /// is rotated and is not recommended for maps that use camera rotation.
    /// </remarks>
    public SamplerState SamplerState { get; set; } = SamplerState.PointClamp;

    /// <summary>
    /// Gets or sets the sprite sort mode used for each SpriteBatch.Begin call.
    /// </summary>
    /// <remarks>
    /// The default is <see cref="SpriteSortMode.Deferred"/>, which preserves draw order within
    /// a layer. <see cref="SpriteSortMode.Texture"/> can reduce GPU state changes for layers
    /// that reference tiles from multiple tilesets, at the cost of rendering order guarantees.
    /// </remarks>
    public SpriteSortMode SpriteSortMode { get; set; } = SpriteSortMode.Deferred;

    /// <summary>
    /// Gets or sets the custom shader effect applied when drawing tiles, or null to use the default.
    /// </summary>
    public Effect Effect { get; set; }

    /// <summary>
    /// Loads a tilemap for rendering.
    /// </summary>
    /// <param name="tilemap">The tilemap to load.</param>
    /// <exception cref="ArgumentNullException">Thrown if tilemap is null.</exception>
    public void LoadTilemap(Tilemap tilemap)
    {
        _tilemap = tilemap ?? throw new ArgumentNullException(nameof(tilemap));
        BuildAnimatedTilesList();
        _tileRenderPadding = ComputeTileRenderPadding(tilemap);
    }

    /// <summary>
    /// Unloads the current tilemap.
    /// </summary>
    public void UnloadTilemap()
    {
        _tilemap = null;
        _animatedTiles.Clear();
        _tileRenderPadding = default;
    }

    /// <summary>
    /// Draws all visible tile layers.
    /// </summary>
    /// <param name="spriteBatch">The sprite batch to draw with.</param>
    /// <param name="camera">The camera providing the view transform and visible bounds.</param>
    /// <exception cref="ArgumentNullException">Thrown if spriteBatch or camera is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if no tilemap is loaded.</exception>
    public void Draw(SpriteBatch spriteBatch, OrthographicCamera camera)
    {
        ArgumentNullException.ThrowIfNull(spriteBatch);
        ArgumentNullException.ThrowIfNull(camera);

        ThrowIfNoTilemap();

        bool inBatch = false;
        Vector2 currentParallax = default;

        foreach (TilemapLayer layer in _tilemap.Layers)
        {
            if (!layer.IsVisible)
            {
                if (inBatch)
                {
                    spriteBatch.End();
                    inBatch = false;
                }

                continue;
            }

            if (layer is TilemapImageLayer imageLayer)
            {
                if (inBatch)
                {
                    spriteBatch.End();
                    inBatch = false;
                }

                DrawImageLayerCore(spriteBatch, camera, imageLayer);
                continue;
            }

            if (layer is TilemapObjectLayer objectLayer)
            {
                if (inBatch)
                {
                    spriteBatch.End();
                    inBatch = false;
                }

                DrawObjectLayerCore(spriteBatch, camera, objectLayer);
                continue;
            }

            if (layer is not TilemapTileLayer tileLayer)
            {
                if (inBatch)
                {
                    spriteBatch.End();
                    inBatch = false;
                }

                continue;
            }

            Vector2 parallax = tileLayer.ParallaxFactor;

            if (!inBatch || currentParallax != parallax)
            {
                if (inBatch)
                {
                    spriteBatch.End();
                }

                currentParallax = parallax;
                BeginLayerBatch(spriteBatch, camera, parallax);
                inBatch = true;
            }

            DrawTileLayerCore(spriteBatch, camera, tileLayer);
        }

        if (inBatch)
        {
            spriteBatch.End();
        }
    }

    /// <summary>
    /// Draws a single tile layer by name.
    /// </summary>
    /// <param name="spriteBatch">The sprite batch to draw with.</param>
    /// <param name="camera">The camera providing the view transform and visible bounds.</param>
    /// <param name="layerName">The name of the layer to draw.</param>
    /// <exception cref="ArgumentNullException">Thrown if spriteBatch, camera, or layerName is null.</exception>
    /// <exception cref="KeyNotFoundException">Thrown if the layer name is not found.</exception>
    /// <exception cref="InvalidOperationException">Thrown if no tilemap is loaded.</exception>
    public void DrawLayer(SpriteBatch spriteBatch, OrthographicCamera camera, string layerName)
    {
        ArgumentNullException.ThrowIfNull(spriteBatch);
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(layerName);

        ThrowIfNoTilemap();

        TilemapLayer layer = _tilemap.Layers[layerName];
        DrawLayerInternal(spriteBatch, camera, layer);
    }

    /// <summary>
    /// Draws a single tile layer by index.
    /// </summary>
    /// <param name="spriteBatch">The sprite batch to draw with.</param>
    /// <param name="camera">The camera providing the view transform and visible bounds.</param>
    /// <param name="layerIndex">The zero-based index of the layer to draw.</param>
    /// <exception cref="ArgumentNullException">Thrown if spriteBatch or camera is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if layerIndex is out of range.</exception>
    /// <exception cref="InvalidOperationException">Thrown if no tilemap is loaded.</exception>
    public void DrawLayer(SpriteBatch spriteBatch, OrthographicCamera camera, int layerIndex)
    {
        ArgumentNullException.ThrowIfNull(spriteBatch);
        ArgumentNullException.ThrowIfNull(camera);

        ThrowIfNoTilemap();

        if (layerIndex < 0 || layerIndex >= _tilemap.Layers.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(layerIndex));
        }

        TilemapLayer layer = _tilemap.Layers[layerIndex];
        DrawLayerInternal(spriteBatch, camera, layer);
    }

    /// <summary>
    /// Draws the rows of a tile layer whose bottom edge falls within a world-space Y range.
    /// </summary>
    /// <param name="spriteBatch">The sprite batch to draw with.</param>
    /// <param name="camera">The camera providing the view transform and visible bounds.</param>
    /// <param name="layerName">The name of the tile layer to draw.</param>
    /// <param name="minY">The inclusive lower bound of the range, in world space.</param>
    /// <param name="maxY">The exclusive upper bound of the range, in world space.</param>
    /// <remarks>
    /// A row is drawn if its bottom edge is within [minY, maxY). Draw the layer in consecutive
    /// ranges with entities drawn between calls to sort them with tiles by Y.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown if spriteBatch, camera, or layerName is null.</exception>
    /// <exception cref="KeyNotFoundException">Thrown if the layer name is not found.</exception>
    /// <exception cref="ArgumentException">Thrown if the layer is not a tile layer.</exception>
    /// <exception cref="NotSupportedException">Thrown if the tilemap is not orthogonal.</exception>
    /// <exception cref="InvalidOperationException">Thrown if no tilemap is loaded.</exception>
    public void DrawLayer(SpriteBatch spriteBatch, OrthographicCamera camera, string layerName, float minY, float maxY)
    {
        ArgumentNullException.ThrowIfNull(spriteBatch);
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(layerName);

        ThrowIfNoTilemap();

        if (_tilemap.Layers[layerName] is not TilemapTileLayer tileLayer)
        {
            throw new ArgumentException($"Layer '{layerName}' is not a tile layer.", nameof(layerName));
        }

        DrawTileLayerRows(spriteBatch, camera, tileLayer, minY, maxY);
    }

    /// <summary>
    /// Draws the rows of a tile layer whose bottom edge falls within a world-space Y range.
    /// </summary>
    /// <param name="spriteBatch">The sprite batch to draw with.</param>
    /// <param name="camera">The camera providing the view transform and visible bounds.</param>
    /// <param name="layerIndex">The zero-based index of the tile layer to draw.</param>
    /// <param name="minY">The inclusive lower bound of the range, in world space.</param>
    /// <param name="maxY">The exclusive upper bound of the range, in world space.</param>
    /// <remarks>
    /// A row is drawn if its bottom edge is within [minY, maxY). Draw the layer in consecutive
    /// ranges with entities drawn between calls to sort them with tiles by Y.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown if spriteBatch or camera is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if layerIndex is out of range.</exception>
    /// <exception cref="ArgumentException">Thrown if the layer is not a tile layer.</exception>
    /// <exception cref="NotSupportedException">Thrown if the tilemap is not orthogonal.</exception>
    /// <exception cref="InvalidOperationException">Thrown if no tilemap is loaded.</exception>
    public void DrawLayer(SpriteBatch spriteBatch, OrthographicCamera camera, int layerIndex, float minY, float maxY)
    {
        ArgumentNullException.ThrowIfNull(spriteBatch);
        ArgumentNullException.ThrowIfNull(camera);

        ThrowIfNoTilemap();

        if (layerIndex < 0 || layerIndex >= _tilemap.Layers.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(layerIndex));
        }

        if (_tilemap.Layers[layerIndex] is not TilemapTileLayer tileLayer)
        {
            throw new ArgumentException($"Layer at index {layerIndex} is not a tile layer.", nameof(layerIndex));
        }

        DrawTileLayerRows(spriteBatch, camera, tileLayer, minY, maxY);
    }

    /// <summary>
    /// Draws one or more tile layers by name, in the order specified.
    /// </summary>
    /// <param name="spriteBatch">The sprite batch to draw with.</param>
    /// <param name="camera">The camera providing the view transform and visible bounds.</param>
    /// <param name="layerNames">The names of the layers to draw.</param>
    /// <remarks>
    /// Consecutive layers with the same parallax factor share a single SpriteBatch.Begin/End pair.
    /// This method is suited for drawing groups of layers with entities interleaved between calls.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown if spriteBatch, camera, or layerNames is null.</exception>
    /// <exception cref="KeyNotFoundException">Thrown if any layer name is not found.</exception>
    /// <exception cref="InvalidOperationException">Thrown if no tilemap is loaded.</exception>
    public void DrawLayers(SpriteBatch spriteBatch, OrthographicCamera camera, params string[] layerNames)
    {
        ArgumentNullException.ThrowIfNull(spriteBatch);
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(layerNames);

        ThrowIfNoTilemap();

        bool inBatch = false;
        Vector2 currentParallax = default;

        foreach (string name in layerNames)
        {
            TilemapLayer layer = _tilemap.Layers[name];

            if (!layer.IsVisible)
            {
                if (inBatch)
                {
                    spriteBatch.End();
                    inBatch = false;
                }

                continue;
            }

            if (layer is TilemapImageLayer imageLayer)
            {
                if (inBatch)
                {
                    spriteBatch.End();
                    inBatch = false;
                }

                DrawImageLayerCore(spriteBatch, camera, imageLayer);
                continue;
            }

            if (layer is TilemapObjectLayer objectLayer)
            {
                if (inBatch)
                {
                    spriteBatch.End();
                    inBatch = false;
                }

                DrawObjectLayerCore(spriteBatch, camera, objectLayer);
                continue;
            }

            if (layer is not TilemapTileLayer tileLayer)
            {
                if (inBatch)
                {
                    spriteBatch.End();
                    inBatch = false;
                }

                continue;
            }

            Vector2 parallax = tileLayer.ParallaxFactor;

            if (!inBatch || currentParallax != parallax)
            {
                if (inBatch)
                {
                    spriteBatch.End();
                }

                currentParallax = parallax;
                BeginLayerBatch(spriteBatch, camera, parallax);
                inBatch = true;
            }

            DrawTileLayerCore(spriteBatch, camera, tileLayer);
        }

        if (inBatch)
        {
            spriteBatch.End();
        }
    }

    /// <summary>
    /// Updates animated tiles.
    /// </summary>
    /// <param name="gameTime">The game time.</param>
    /// <exception cref="ArgumentNullException">Thrown if gameTime is null.</exception>
    public void Update(GameTime gameTime)
    {
        ArgumentNullException.ThrowIfNull(gameTime);

        if (_animatedTiles.Count == 0)
        {
            return;
        }

        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        for (int i = 0; i < _animatedTiles.Count; i++)
        {
            _animatedTiles[i].Animation.Update(dt);
        }
    }

    private void DrawLayerInternal(SpriteBatch spriteBatch, OrthographicCamera camera, TilemapLayer layer)
    {
        if (!layer.IsVisible)
        {
            return;
        }

        if (layer is TilemapImageLayer imageLayer)
        {
            DrawImageLayerCore(spriteBatch, camera, imageLayer);
            return;
        }

        if (layer is TilemapObjectLayer objectLayer)
        {
            DrawObjectLayerCore(spriteBatch, camera, objectLayer);
            return;
        }

        if (layer is TilemapTileLayer tileLayer)
        {
            BeginLayerBatch(spriteBatch, camera, tileLayer.ParallaxFactor);
            DrawTileLayerCore(spriteBatch, camera, tileLayer);
            spriteBatch.End();
        }
    }

    private void DrawTileLayerRows(SpriteBatch spriteBatch, OrthographicCamera camera, TilemapTileLayer tileLayer, float minY, float maxY)
    {
        if (_tilemap.Orientation != TilemapOrientation.Orthogonal)
        {
            throw new NotSupportedException("Drawing a range of rows is only supported for orthogonal tilemaps.");
        }

        if (!tileLayer.IsVisible)
        {
            return;
        }

        Rectangle visibleRegion = ComputeVisibleTileRegion(camera, tileLayer);
        Rectangle region = TilemapRendererShared.ClampTileRegionToRows(visibleRegion, tileLayer, minY, maxY);

        BeginLayerBatch(spriteBatch, camera, tileLayer.ParallaxFactor);
        DrawTileRegion(spriteBatch, tileLayer, region);
        spriteBatch.End();
    }

    private void DrawImageLayerCore(SpriteBatch spriteBatch, OrthographicCamera camera, TilemapImageLayer imageLayer)
    {
        if (imageLayer.Texture == null)
        {
            return;
        }

        BeginLayerBatch(spriteBatch, camera, imageLayer.ParallaxFactor);

        Texture2D texture = imageLayer.Texture;
        int texW = texture.Width;
        int texH = texture.Height;
        Rectangle src = new Rectangle(0, 0, texW, texH);
        Vector2 offset = imageLayer.Offset;

        Color color = imageLayer.TintColor.HasValue
            ? imageLayer.TintColor.Value * imageLayer.Opacity
            : Color.White * imageLayer.Opacity;

        if (!imageLayer.RepeatX && !imageLayer.RepeatY)
        {
            spriteBatch.Draw(texture, imageLayer.Position + offset, src, color);
        }
        else
        {
            RectangleF camBounds = camera.BoundingRectangle;
            Vector2 parallax = imageLayer.ParallaxFactor;
            Vector2 parallaxOrigin = _tilemap.ParallaxOrigin;

            // Parallax shifts which part of the layer is visible, using the same formula as ComputeVisibleTileRegion.
            float visLeft = parallaxOrigin.X + (camBounds.X - parallaxOrigin.X) * parallax.X - offset.X;
            float visTop = parallaxOrigin.Y + (camBounds.Y - parallaxOrigin.Y) * parallax.Y - offset.Y;

            float startX = imageLayer.RepeatX
                ? (float)Math.Floor((visLeft - imageLayer.Position.X) / texW) * texW + imageLayer.Position.X
                : imageLayer.Position.X;
            float startY = imageLayer.RepeatY
                ? (float)Math.Floor((visTop - imageLayer.Position.Y) / texH) * texH + imageLayer.Position.Y
                : imageLayer.Position.Y;

            // endX/Y are inclusive stopping bounds: one tile width/height past the visible edge.
            // For the non-repeat axis, +1 guarantees exactly one iteration.
            float endX = imageLayer.RepeatX ? visLeft + camBounds.Width + texW : startX + 1f;
            float endY = imageLayer.RepeatY ? visTop + camBounds.Height + texH : startY + 1f;

            for (float y = startY; y < endY; y += texH)
            {
                for (float x = startX; x < endX; x += texW)
                {
                    spriteBatch.Draw(texture, new Vector2(x, y) + offset, src, color);
                }
            }
        }

        spriteBatch.End();
    }

    private void DrawObjectLayerCore(SpriteBatch spriteBatch, OrthographicCamera camera, TilemapObjectLayer objectLayer)
    {
        BeginLayerBatch(spriteBatch, camera, objectLayer.ParallaxFactor);

        Color layerColor = objectLayer.TintColor.HasValue
            ? objectLayer.TintColor.Value * objectLayer.Opacity
            : Color.White * objectLayer.Opacity;

        foreach (TilemapObject obj in objectLayer.Objects)
        {
            if (!obj.IsVisible)
            {
                continue;
            }

            if (obj is not TilemapTileObject tileObj)
            {
                continue;
            }

            int localId = tileObj.Tile.GetLocalId(_tilemap.Tilesets, out TilemapTileset tileset);

            if (tileset == null)
            {
                continue;
            }

            tileset.GetRenderSource(localId, out Texture2D texture, out Rectangle sourceRect);

            if (texture == null)
            {
                continue;
            }

            Vector2 scale = new Vector2(
                tileObj.Size.X / sourceRect.Width,
                tileObj.Size.Y / sourceRect.Height);

            SpriteEffects effects = SpriteEffects.None;
            TilemapTileFlipFlags flip = tileObj.Tile.FlipFlags;
            if ((flip & TilemapTileFlipFlags.FlipHorizontally) != 0)
            {
                effects |= SpriteEffects.FlipHorizontally;
            }

            if ((flip & TilemapTileFlipFlags.FlipVertically) != 0)
            {
                effects |= SpriteEffects.FlipVertically;
            }

            // Tiled positions tile objects at their bottom-left corner.
            // An origin of (0, sourceRect.Height) places that pivot at tileObj.Position in world space,
            // so the tile draws upward from there as expected.
            // Round to integer world pixels so sub-pixel object positions don't cause the linear
            // filter to blend anti-aliased edge texels with their transparent neighbors, which
            // would produce dark fringe artifacts where objects overlap.
            Vector2 drawPos = new Vector2(MathF.Round(tileObj.Position.X), MathF.Round(tileObj.Position.Y));
            spriteBatch.Draw(
                texture,
                drawPos,
                sourceRect,
                layerColor,
                tileObj.Rotation,
                new Vector2(0f, sourceRect.Height),
                scale,
                effects,
                0f);
        }

        spriteBatch.End();
    }

    private void BeginLayerBatch(SpriteBatch spriteBatch, OrthographicCamera camera, Vector2 parallax)
    {
        Matrix view = camera.GetViewMatrix();

        if (parallax != Vector2.One)
        {
            // Correct parallax: scroll relative to the viewport top-left (camBounds), not camera.Position.
            // The parallax offset in world space is (1 - F) * (TL - parallaxOrigin), left-multiplied
            // into the view matrix so it acts as a pre-transform in world space.
            RectangleF camBounds = camera.BoundingRectangle;
            Vector2 tl = new Vector2(camBounds.X, camBounds.Y);
            Vector2 parallaxOrigin = _tilemap.ParallaxOrigin;
            Vector2 offset = (Vector2.One - parallax) * (tl - parallaxOrigin);
            view = Matrix.CreateTranslation(offset.X, offset.Y, 0f) * view;
        }

        // Snap the view translation to integer pixels to prevent sub-pixel sampling artifacts.
        // When the camera is at a fractional position, linear filtering blends edge texels with
        // the transparent background, producing dark fringe artifacts that flicker as the camera moves.
        view.M41 = MathF.Round(view.M41);
        view.M42 = MathF.Round(view.M42);

        spriteBatch.Begin(SpriteSortMode, BlendState, SamplerState, null, null, Effect, view);
    }

    private void DrawTileLayerCore(SpriteBatch spriteBatch, OrthographicCamera camera, TilemapTileLayer tileLayer)
    {
        Rectangle visibleRegion = ComputeVisibleTileRegion(camera, tileLayer);
        DrawTileRegion(spriteBatch, tileLayer, visibleRegion);
    }

    private void DrawTileRegion(SpriteBatch spriteBatch, TilemapTileLayer tileLayer, Rectangle region)
    {
        if (region.Width <= 0 || region.Height <= 0)
        {
            return;
        }

        Color layerColor = ComputeLayerColor(tileLayer);

        foreach (TilemapTileEntry entry in tileLayer.GetTilesInRegion(region))
        {
            int localId = entry.Tile.GetLocalId(_tilemap.Tilesets, out TilemapTileset tileset);

            if (tileset == null)
            {
                continue;
            }

            tileset.GetRenderSource(localId, out Texture2D tileTexture, out Rectangle sourceRect);

            if (tileTexture == null)
            {
                continue;
            }

            Point worldPos = _tilemap.TileToWorldPosition(entry.X, entry.Y);
            Vector2 drawPosition = new Vector2(worldPos.X, worldPos.Y) + tileLayer.Offset + tileset.TileOffset;

            // Tiled bottom-aligns all tiles: shifts oversized tiles up and undersized tiles down.
            drawPosition.Y += tileLayer.TileHeight - sourceRect.Height;

            DrawTile(spriteBatch, tileTexture, drawPosition, sourceRect,
                entry.Tile.FlipFlags, sourceRect.Width, sourceRect.Height, layerColor);
        }
    }

    private Rectangle ComputeVisibleTileRegion(OrthographicCamera camera, TilemapTileLayer tileLayer)
    {
        RectangleF camBounds = camera.BoundingRectangle;
        Vector2 parallax = tileLayer.ParallaxFactor;
        Vector2 parallaxOrigin = _tilemap.ParallaxOrigin;

        // The visible region's top-left in world space for a parallax layer is the point that
        // maps to the screen top-left after the parallax transform. With origin O and factor F,
        // the effective world left is O + F * (TL - O), where TL is the viewport top-left.
        float worldLeft = parallaxOrigin.X + (camBounds.X - parallaxOrigin.X) * parallax.X - tileLayer.Offset.X;
        float worldTop = parallaxOrigin.Y + (camBounds.Y - parallaxOrigin.Y) * parallax.Y - tileLayer.Offset.Y;

        // Oversized tiles can render outside their owning cell, especially image-collection
        // tiles and tilesets with tile offsets. Expand the queried tile region by the maximum
        // render overhang so partially visible tiles are not culled too early at the viewport edge.
        // https://github.com/MonoGame-Extended/Monogame-Extended/issues/1139
        int startX = Math.Max(0, (int)Math.Floor((worldLeft - _tileRenderPadding.Right) / tileLayer.TileWidth));
        int startY = Math.Max(0, (int)Math.Floor((worldTop - _tileRenderPadding.Bottom) / tileLayer.TileHeight));
        int endX = Math.Min(tileLayer.Width, (int)Math.Ceiling((worldLeft + camBounds.Width + _tileRenderPadding.Left) / tileLayer.TileWidth));
        int endY = Math.Min(tileLayer.Height, (int)Math.Ceiling((worldTop + camBounds.Height + _tileRenderPadding.Top) / tileLayer.TileHeight));

        return new Rectangle(startX, startY, endX - startX, endY - startY);
    }

    // https://github.com/MonoGame-Extended/Monogame-Extended/issues/1139
    private static TileRenderPadding ComputeTileRenderPadding(Tilemap tilemap)
    {
        float left = 0f;
        float top = 0f;
        float right = 0f;
        float bottom = 0f;

        foreach (TilemapTileset tileset in tilemap.Tilesets)
        {
            float tileLeft = tileset.TileOffset.X;
            float tileTop = tileset.TileOffset.Y + tilemap.TileHeight - tileset.TileHeight;
            float tileRight = tileset.TileOffset.X + tileset.TileWidth;
            float tileBottom = tileset.TileOffset.Y + tilemap.TileHeight;

            left = Math.Max(left, Math.Max(0f, -tileLeft));
            top = Math.Max(top, Math.Max(0f, -tileTop));
            right = Math.Max(right, Math.Max(0f, tileRight - tilemap.TileWidth));
            bottom = Math.Max(bottom, Math.Max(0f, tileBottom - tilemap.TileHeight));
        }

        return new TileRenderPadding(left, top, right, bottom);
    }

    private static Color ComputeLayerColor(TilemapLayer layer)
    {
        if (layer.TintColor.HasValue)
        {
            return layer.TintColor.Value * layer.Opacity;
        }

        return Color.White * layer.Opacity;
    }

    private static void DrawTile(SpriteBatch spriteBatch, Texture2D texture, Vector2 position, Rectangle sourceRect, TilemapTileFlipFlags flipFlags, int tileWidth, int tileHeight, Color color)
    {
        bool flipH = (flipFlags & TilemapTileFlipFlags.FlipHorizontally) != 0;
        bool flipV = (flipFlags & TilemapTileFlipFlags.FlipVertically) != 0;
        bool flipD = (flipFlags & TilemapTileFlipFlags.FlipDiagonally) != 0;

        // Scale maps source rect dimensions to the tile's destination size.
        // For standard tilemaps this is (1,1) but may differ when the tileset tile size
        // does not match the layer tile size.
        Vector2 scale = new Vector2(
            (float)tileWidth / sourceRect.Width,
            (float)tileHeight / sourceRect.Height);

        if (!flipD)
        {
            SpriteEffects effects = SpriteEffects.None;
            if (flipH)
            {
                effects |= SpriteEffects.FlipHorizontally;
            }

            if (flipV)
            {
                effects |= SpriteEffects.FlipVertically;
            }

            spriteBatch.Draw(texture, position, sourceRect, color, 0f, Vector2.Zero, scale, effects, 0f);
        }
        else
        {
            // Diagonal flip encodes 90-degree rotations per Tiled's convention.
            // The four diagonal combinations map to specific rotation + flip pairs:
            //   H=0 V=1 D=1  -> rotate 90 degrees counterclockwise
            //   H=1 V=0 D=1  -> rotate 90 degrees clockwise
            //   H=1 V=1 D=1  -> rotate 90 degrees clockwise, flip horizontal
            //   H=0 V=0 D=1  -> rotate 90 degrees counterclockwise, flip horizontal
            float rotation;
            SpriteEffects effects;

            if (!flipH && flipV)
            {
                rotation = -MathHelper.PiOver2;
                effects = SpriteEffects.None;
            }
            else if (flipH && !flipV)
            {
                rotation = MathHelper.PiOver2;
                effects = SpriteEffects.None;
            }
            else if (flipH && flipV)
            {
                rotation = MathHelper.PiOver2;
                effects = SpriteEffects.FlipHorizontally;
            }
            else
            {
                rotation = -MathHelper.PiOver2;
                effects = SpriteEffects.FlipHorizontally;
            }

            // Rotation pivots around the tile's center in source space. Position is moved
            // to the world-space tile center so the tile renders in its correct grid cell.
            Vector2 origin = new Vector2(sourceRect.Width * 0.5f, sourceRect.Height * 0.5f);
            Vector2 center = position + new Vector2(tileWidth * 0.5f, tileHeight * 0.5f);

            spriteBatch.Draw(texture, center, sourceRect, color, rotation, origin, scale, effects, 0f);
        }
    }

    private void BuildAnimatedTilesList()
    {
        _animatedTiles.Clear();
        foreach (TilemapTileset tileset in _tilemap.Tilesets)
        {
            IReadOnlyList<TilemapTileData> animated = tileset.GetAnimatedTiles();
            for (int i = 0; i < animated.Count; i++)
            {
                _animatedTiles.Add(animated[i]);
            }
        }
    }

    private void ThrowIfNoTilemap()
    {
        if (_tilemap == null)
        {
            throw new InvalidOperationException("No tilemap loaded. Call LoadTilemap first.");
        }
    }

    private readonly record struct TileRenderPadding(float Left, float Top, float Right, float Bottom);
}
