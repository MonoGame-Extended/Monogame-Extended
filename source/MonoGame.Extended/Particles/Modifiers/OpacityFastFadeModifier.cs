// Copyright (c) Craftwork Games. All rights reserved.
// Licensed under the MIT license.
// See LICENSE file in the project root for full license information.

using MonoGame.Extended.Particles.Data;

namespace MonoGame.Extended.Particles.Modifiers;

/// <summary>
/// A modifier that rapidly decreases particle opacity based on their age.
/// </summary>
/// <remarks>
/// The <see cref="OpacityFastFadeModifier"/> creates a linear fade-out effect where particles
/// become more transparent as they age.
///
/// Important notes:
/// <list type="bullet">
///   <item>
///     <see cref="Particle.Age"/> is normalized to the emitter's configured lifespan, so the fade
///     completes exactly at end of life regardless of the particle's actual lifespan in seconds.
///   </item>
///   <item>
///     Unlike other modifiers that accumulate changes over time, this modifier directly sets
///     the opacity value each frame based on the particle's age and initial opacity.
///   </item>
///   <item>
///     <see cref="Particle.InitialOpacity"/> must be initialized to the desired release-time opacity.
///     <see cref="ParticleEmitter"/> does this automatically; callers that use
///     <see cref="ParticleBuffer"/> directly must also initialize <see cref="Particle.Opacity"/>.
///   </item>
/// </list>
/// </remarks>
public sealed class OpacityFastFadeModifier : Modifier
{
    /// <summary>
    /// Updates all particles by setting their opacity based on their age and initial opacity.
    /// </summary>
    /// <inheritdoc/>
    protected internal override unsafe void Update(float elapsedSeconds, ParticleIterator iterator, int particleCount)
    {
        if (!Enabled) { return; }

        for (int i = 0; i < particleCount && iterator.HasNext; i++)
        {
            Particle* particle = iterator.Next();

            particle->Opacity = particle->InitialOpacity * (1.0f - particle->Age);
        }
    }
}
