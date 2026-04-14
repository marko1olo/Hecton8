# Music Loudness Audit

Status: `PENDING VERIFICATION`

Meaning:

- `MeanDb`: ffmpeg `volumedetect` mean volume
- `VolumeScale`: runtime attenuation written into `HectonMusicClip._volume`
- low `VolumeScale` means the track was louder than category target and got turned down
- tracks that stay at `1.0` are not automatically "correct"; they just were not attenuated

Category targets:

- `bed`: -15.3 dB
- `base`: -15.55 dB
- `menu`: -15.05 dB
- `combat`: -16.55 dB
- `stinger`: -15.05 dB

Manual audit candidates: quietest mean-volume tracks

- `ambient_korotki_2_Sub-Bass Throb (1)`: MeanDb=-18.7 dB | VolumeScale=1
- `abyss_termal_2_Steam-Teeth Ritual`: MeanDb=-17.2 dB | VolumeScale=1
- `melkovodie_3_Underwater Reverbfall`: MeanDb=-17.2 dB | VolumeScale=1
- `shelf_9_Deep-Sea Tapeflutter (1)`: MeanDb=-17 dB | VolumeScale=1
- `dead_reefs_2_Aquatic Hissdrift (1)`: MeanDb=-16.9 dB | VolumeScale=1
- `warm_base_lofi_6_Vinyl Warmth Echo`: MeanDb=-16.9 dB | VolumeScale=1
- `being_attacked_2_Metallic Alarm Panic (1)`: MeanDb=-16.8 dB | VolumeScale=1
- `warm_base_lofi_7_Vinyl Warmth Echo (1)`: MeanDb=-16.8 dB | VolumeScale=1
- `ambient_deep_1_Underwater Muffled Silence`: MeanDb=-16.1 dB | VolumeScale=1
- `abyss_1_Pitch-Black Silence`: MeanDb=-16.1 dB | VolumeScale=1
- `being_attacked_1_Metallic Alarm Panic`: MeanDb=-16 dB | VolumeScale=0.939
- `cave_ambient_4_Sub-bass Pressure`: MeanDb=-16 dB | VolumeScale=1
- `being_attacked_3_Metallic Alarm`: MeanDb=-16 dB | VolumeScale=0.939
- `danger_zatmenie_1_Distant Metallic Groan`: MeanDb=-15.8 dB | VolumeScale=0.917
- `abyss_termal_3_Steam-Teeth Ritual (1)`: MeanDb=-15.6 dB | VolumeScale=1

Manual audit candidates: most attenuated tracks

- `shelf_1_Abandoned Depths`: MeanDb=-9.1 dB | VolumeScale=0.49
- `danger_zatmenie_2_Abandoned Depths`: MeanDb=-12.8 dB | VolumeScale=0.649
- `ambient_dlinni_2_Sub-Bass Hive`: MeanDb=-12.1 dB | VolumeScale=0.692
- `cave_ambient_3_Sub-bass Pressure`: MeanDb=-12.2 dB | VolumeScale=0.7
- `dead_reefs_1_Aquatic Hissdrift`: MeanDb=-12.2 dB | VolumeScale=0.7
- `shelf_8_Deep-Sea Tapeflutter`: MeanDb=-12.4 dB | VolumeScale=0.716
- `ambient_korotki_3_Sub-Bass Throb (2)`: MeanDb=-12.5 dB | VolumeScale=0.724
- `ambient_korotki_1_Sub-Bass Throb`: MeanDb=-12.5 dB | VolumeScale=0.724
- `cave_ambient_6_Vinyl Underwater Ruins`: MeanDb=-12.7 dB | VolumeScale=0.741
- `shelf_2_Abandoned Depths (1)`: MeanDb=-12.8 dB | VolumeScale=0.75
- `shelf_7_Decaying Analog Static`: MeanDb=-13.2 dB | VolumeScale=0.785
- `ambient_dlinni_1_Sub-Bass Throb`: MeanDb=-13.3 dB | VolumeScale=0.794
- `being_attacked_4_Metallic Alarm (1)`: MeanDb=-14.9 dB | VolumeScale=0.827
- `warm_base_lofi_3_Underwater Hum`: MeanDb=-13.9 dB | VolumeScale=0.827
- `cave_ambient_7_Vinyl Underwater Ruins (1)`: MeanDb=-13.8 dB | VolumeScale=0.841
