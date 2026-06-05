# Audio Policy Conflict And Cue Disposition - Asset Worker 3213 - 2026-06-05

Status: `PENDING VERIFICATION`
Evidence boundary: `STATIC_SOURCE` / `AUDIO_WAVEFORM_QA` only.
First-20 route moment: first exit, first surface/shallow read, photic shelf orientation, warning audibility.

This is not Unity import acceptance. This is not runtime mix acceptance. This is not 0 B/frame proof. This is not audio-thread safety proof. This is not final loudness proof.

## Sources Read

- `AGENTS.md`
- `Docs/AssetAudit/ASSET_SYSTEM_INDEX_20260605.md`
- `Docs/AssetAudit/AUDIO_WAVEFORM_REVIEW_20260605.md`
- `Docs/AssetAudit/AUDIO_ASSET_STATIC_LEDGER_20260605.csv`
- `Docs/Audio/audio_asset_ledger.csv`
- `audio.md`
- `streaming.md`
- `.agents-skills/AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC.txt`
- `.agents-skills/STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`

Mandates followed:

- `AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC`: no managed callback or DSP acceptance claim from static evidence.
- `STRM_Asset_Lifecycle_Addressables_Loading_Memory`: asset ownership, stable keys, ref-count and residency proof remain unresolved until Addressables evidence exists.

## Ledger Edits

Edited: `Docs/Audio/audio_asset_ledger.csv`.

- Replaced the ambiguous `sfx_or_player_loop` class split with `sfx` and `player_loop`.
- Preserved every `PENDING_OWNER`, `PENDING_ADDRESSABLES` group, and `PENDING_ADDRESSABLES` key field.
- Kept 138 ledger rows matched to the static source inventory paths.
- Current class counts: 84 `music`, 30 `sfx`, 12 `ambient`, 5 `ui`, 5 `player_loop`, 2 `voice`.
- Marked breath, suit, and swim rows as player-loop candidates, not generic SFX:
  - `BREATHING_BREATH_IN_AND_OUT_1`
  - `BREATHING_RHYTMIC_8S_3_BREATH_IN_AND_3_OUT`
  - `INSIDE_SUIT_SOUNDS_TOO_LOUD`
  - `SWIMMING_UNDERWATER`
  - `SWIMMING_ONWATER`
- Tightened first-exit/shallow route notes for selected ambient/music/stinger/UI rows without changing import settings.
- Marked VO stubs as placeholder/audio-log stubs with duration override/localization proof pending.

## Unresolved Import Policy Conflict

`AGENTS.md` states ambient/music should use Vorbis Q70 and `Compressed In Memory`, with streaming music only. `streaming.md` and `STRM_Asset_Lifecycle_Addressables_Loading_Memory` define duration-based audio load policy:

- duration greater than 10 seconds: Streaming
- short SFX up to 2 seconds: DecompressOnLoad
- medium 2 to 10 seconds: CompressedInMemory

This is unresolved policy conflict. I did not edit `.meta` files, AudioClip imports, clip assets, Addressables groups, or Unity settings.

Current static conflict surface:

- 84 music clips are Streaming + Vorbis + Q0.7 in static metadata.
- Ambient rows mix Streaming and CompressedInMemory; multiple Streaming ambient clips are Q0.45, below the Q70 target.
- Long player-layer loops include Streaming rows. They are now classified as `player_loop`, but import disposition is still unresolved.
- Generic SFX must not be Streaming; this pass found the long streaming risks are player loops, not one-shot SFX.

Required owner decision:

- Decide whether audio import policy follows AGENTS ambient/music compressed-in-memory language, the streaming duration rule, or a route-specific exception table.
- Record the decision in stable authority before mass import edits.
- Then run Unity import proof, Memory Profiler, mix proof, and Addressables residency proof.

## Cue Disposition

### First-Exit And Shallow Candidate Cues

Candidate rows now tagged in the ledger:

- `UNDERWATER_AMBIENT`: first-exit/shallow underwater-bed candidate. Waveform QA shows broad bed with late swell; needs ducking proof.
- `ATMOS_1_LOOP`: first-exit/shallow dense-bed candidate. Waveform QA marks masking risk.
- `SPACESHIP_SOUNDS_AMBIENT`: first-exit/shallow pressure-bed candidate. Lower steady bed; import quality below target.
- `SHELF_1_ABANDONED_DEPTHS`: first-exit/shallow shelf MusicDirector candidate, not constant bed. Waveform QA marks loud dense bed.
- `MELKOVODIE_1_VINYL_KELP_GLOW`: shallow/photic MusicDirector candidate.
- `MELKOVODIE_2_THE_WEIGHT_OF_BLUE_LIGHT`: shallow/photic MusicDirector candidate.
- `MELKOVODIE_6_SHORT30SEC_BENEATH_THE_GLASS_GROVE`: short shallow/photic MusicDirector candidate.
- `STINGER_BEING_SAVED_3_FIRST_BREATH_ABOVE`: first-surface/first-breath stinger candidate.
- `STINGER_DANGEROUS_1_IRON_TEETH`: danger/tension stinger candidate. Waveform QA marks it loud/dense.

These are cue candidates only. No listening pass, scene route proof, mix state proof, MusicDirector proof, or runtime acceptance exists here.

### Player Loop Risks

- `BREATHING_BREATH_IN_AND_OUT_1`: waveform QA peak is hot; not generic SFX.
- `INSIDE_SUIT_SOUNDS_TOO_LOUD`: long player-layer loop risk; filename itself is review debt.
- `SWIMMING_ONWATER`: quiet sparse motion loop; player movement ownership pending.
- `SWIMMING_UNDERWATER`: player movement loop candidate; streaming/player-layer risk remains.

Required next proof:

- Player movement/breath source facts.
- Suppression and priority behavior.
- Import policy decision.
- Runtime listening pass through actual output chain.

### VO Placeholder Risk

Rows:

- `VOSTUB_CHEN_LOG01_EN`
- `VOSTUB_CHEN_LOG01_RU`

Static facts:

- Both are 1.341 second stubs.
- Both remain `placeholder_flag=true`.
- Static index reports `AudioLog_chen_m_datapad_01.asset` references these stubs while duration override risk is pending.
- Waveform QA marks the VO stub effectively tiny in preview scale.

Disposition: placeholder only. Do not use these for final dialogue duration, localization, loudness, subtitle timing, or VO delivery proof.

### UI And Warning Audibility Risk

`CLICK_SOUND` is now tagged as `UI feedback/listening-risk cue`. Waveform QA marks it as a very small transient. No warning bank, warning ducking, priority lane, haptic/UI pairing, or audibility proof exists in this evidence boundary.

Warning acceptance blockers:

- Warning source facts not mapped.
- Warning priority and spam suppression not proven.
- Music/ambient ducking not proven.
- Listening pass absent.
- Runtime mix snapshots absent.

## Route Blockers

- MusicDirector gating: static profile route exists, but runtime behavior is unproven.
- MusicDirector mixer routing: static index reports null `_musicMixerGroup` and `_stingerMixerGroup` refs in `MusicDirectorConfig_Global.asset`.
- Silence windows: static index says profile data has pause windows, but no runtime silence-window proof exists.
- Stinger cooldown: loud/dense stingers require cooldown and mix-priority proof.
- Warning audibility: UI click is weak in waveform QA; no warning bank proof exists.
- Addressables owner/key gaps: all 138 rows still have `PENDING_OWNER`, `PENDING_ADDRESSABLES`, and `PENDING_ADDRESSABLES`.
- Direct prefab clip references exist in static evidence, including player prefab refs; Addressables ownership/release proof is absent.
- Import policy conflict is unresolved, so mass import edits are blocked.

## Scalability Consequences

Low/compact:

- Preserve breath, warning, route, and threat cues before music density.
- One active ambient bank max under pressure remains plausible from streaming doctrine, but not accepted until policy and residency proof exist.
- Do not stream generic one-shot SFX.

Middle:

- Add richer shallow ambience only after warning ducking and MusicDirector gating are proven.
- Keep player loops classified separately from SFX for priority and memory policy.

High:

- Spend additional budget on stronger hydrophone/route layers, not constant beds.
- Use richer MusicDirector transitions only if silence windows and stinger cooldowns remain intact.

Ultra:

- Add dense secondary beds, convolution/reverb, and layered stingers only after critical warning and player-loop audibility remain proven.
- GlobalQualityWeight may scale bank breadth, layer count, spatialization, and diagnostic depth; it must not change cue truth ownership, save identity, or Addressables owner route.

## Regression Model

- CPU: no runtime code changed. CPU impact unmeasured and not claimed.
- GC: no runtime code changed. 0 B/frame proof absent.
- Memory: no clip imports, Addressables groups, or residency settings changed. Memory impact unmeasured.
- Cadence: no runtime cadence changed.
- Correctness: static ledger taxonomy improved; runtime source facts, owner keys, and mix routing remain unresolved.

## Final Disposition

Production cue ownership is still blocked by policy and proof gaps. The static ledger is tighter, but all cue/import/runtime acceptance remains `PENDING VERIFICATION`.
