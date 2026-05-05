////////////////////////////////////////////////////////////
//                                                        //
//  Catalog of the Solar system objects for SpaceEngine   //
//                                                        //
// Data and textures from various sources and authors     //
// (look for the comments in the code for reference)      //
// Latest revision:  26 April 2016                        //
//                                                        //
// This catalog contains the most big or famous objects   //
// in the Solar system: planets, dwarf planets, moons,    //
// and 5 biggest asteroids.                               //
//                                                        //
////////////////////////////////////////////////////////////

Star	"Sun/Sol"
{
	ParentBody "Solar System"

	Class      "G2V"
	Luminosity  1.0
	Temperature 5778
	Age         4.57
	FeH         0.0

	MassSol     1.0
	RadSol      1.0
	Oblateness  9e-6
	
	PoleRA          12.075
	PoleDec        -63.87
	ZeroMeridian    0.0
	RotationPeriod  609.12
}

Planet	"Mercury"
{
	ParentBody     "Sol"
	Class          "Selena"

	Mass            0.05528
	Radius          2440

	RotationPeriod  1407.509405
	RotationOffset  291.2
	Obliquity       7.01
	EqAscendNode    48.42

	Albedo          0.068
	AlbedoBond      0.068
	AlbedoGeom	    0.142
	Brightness      4 // 2.3

	Surface
	{
		// Surface map author: Robert Skuridin "PBC"
		DiffMap        "Mercury/Surface-PBC"
		DiffTileSize    258
		DiffTileBorder  1
		DiffMapAlpha   "None"

		// Elevation map author: Sean Young "HarbingerDawn"
		BumpMap        "Mercury/Bump-HD"
		BumpTileSize    258
		BumpTileBorder  1
		BumpHeight      10.289
		BumpOffset		5.889

		Hapke           1.0
		ModulateColor  (1.0 0.97 0.94 1.0)
	}

	NoAtmosphere    true
	NoAurora        true
	NoRings         true

	Orbit
	{
		RefPlane       "Ecliptic"
		Period          0.2408
		SemiMajorAxis   0.3871
		Eccentricity    0.2056
		Inclination     7.0049
		AscendingNode   48.33167
		LongOfPericen   77.456
		MeanLongitude   252.251
		ArgOfPeriPreces 227000 // period of precession in years
	}
}

Planet	"Venus"
{
	ParentBody     "Sol"
	Class          "Desert"

	Mass            0.815
	Radius          6052

	RotationPeriod  5832.479839
	RotationOffset  137.45
	Obliquity       178.78
	EqAscendNode    300.22

	Albedo          0.90
	AlbedoBond      0.90
	AlbedoGeom      0.67
	Brightness      1.9

	Surface
	{
		// Surface map author: John van Vliet
		DiffMap        "Venus/Surface-JVV"
		DiffTileSize    258
		DiffTileBorder  1
		DiffMapAlpha   "None"

		// Elevation map author: Vladimir Romanyuk "SpaceEngineer"
		BumpMap        "Venus/Bump-SE"
		BumpTileSize    258
		BumpTileBorder  1
		BumpHeight      13.7
		BumpOffset		2.9

		Hapke           0.0
		ModulateColor  (1.0 0.84 0.70 1.0)
	}

	Clouds
	{
		DiffMap      "Venus-clouds.*"
		DiffMapAlpha "None"
		DayAmbient    3.0
		Height        50
		Velocity     -264 // km/h
		ModulateColor  (0.35 0.35 0.35 1.0)
	}

	Atmosphere
	{
		Height      200		// km
		Greenhouse  560		// degrees K
		Pressure    92		// atm
		Density     71.4	// kg/m^3
		Model      "Venus"
		Bright      10.0
		Opacity     1.0
		SkyLight    3.0
		
		Composition // values in percent
		{
			CO2 96.5
			N2  3.5
			SO2 0.015
			SO2 0.015
			Ar  0.007
			H2O 0.002
		}
	}

	NoAurora    true
	NoRings		true

	Orbit
	{
		RefPlane       "Ecliptic"
		Period          0.6152
		SemiMajorAxis   0.7233
		Eccentricity    0.0068
		Inclination     3.3947
		AscendingNode   76.681
		LongOfPericen   131.533
		MeanLongitude   181.979
	}
}

Barycenter	"Earth-Moon"
{
	ParentBody     "Sol"
	Orbit
	{
		RefPlane      "Ecliptic"
		Period         1.0          // years
		SemiMajorAxis  1.0000010178 // a.u.
		Eccentricity   0.0167086342
		Inclination    0.0          // degrees
		AscendingNode  348.739      // degrees
		LongOfPericen  102.93734808 // degrees
		MeanLongitude  100.46645683 // degrees
	}
}

Planet	"Earth"
{
	ParentBody     "Earth-Moon"
	Class          "Terra"

	Mass            1.0			// Earth's masses
	Radius          6378.14     // km
	Oblateness      0.00335

	Age	            4.54        // billions years

	RotationPeriod  23.9344694  // hours
	Precession      25592       // period of precession in years
	Obliquity       23.4392911  // degrees
	EqAscendNode    180.0		// degrees
	RotationOffset -79.5        // degrees
	//PoleRA          0.0  		// degrees
	//PoleDec         90.0		// degrees
	//ZeroMeridian    190.16	// degrees

	Color         ( 0.850 0.850 1.000 )
	Albedo          0.306
	AlbedoBond      0.306
	AlbedoGeom      0.367
	Brightness      1.4

	Life
	{
		Class   "Organic"
		Type    "Multicellular"
		Biome   "Marine/Terrestrial"
	}

	Surface
	{
		// Surface map author: Robert Skuridin "PBC"
		DiffMap        "Earth/Surface-PBC"
		DiffTileSize    258
		DiffTileBorder  1
		DiffMapAlpha   "Water"

		// Elevation map author: Robert Skuridin "PBC"
		BumpMap        "Earth/Bump-PBC"
		BumpTileSize    258
		BumpTileBorder  1
		BumpHeight      19.475 // = 10896 + 8579 meters of total elevation span
		BumpOffset      10.896 // = 10896 meters below sea level

		// City lights map author: Sean Young "HarbingerDawn"
		GlowMap        "Earth/Lights-HD"
		GlowTileSize    258
		GlowTileBorder  1
		GlowMode       "Night"
		GlowColor      (1.00 0.90 0.66)
		GlowBright      1.0

		SpecBrightIce   0.0
		SpecBrightWater 0.35
		colorSea       (0.04 0.10 0.20 1.00)
		colorShelf     (0.15 0.48 0.46 1.00)
	}

	Ocean
	{
		Height 10.896
	}

	Clouds
	{
		// Clouds map author: Vladimir Romanyuk "SpaceEngineer"
		DiffMap        "Earth/Clouds-SE"
		DiffTileSize    258
		DiffTileBorder  1
		BumpMap        "Earth/Clouds-SE"
		BumpTileSize    258
		BumpTileBorder  1
		BumpHeight      2.5
		BumpOffset      0.0
		DayAmbient      3.0
		Hapke           0.2
		Height          9.0 //3.5
		Velocity        0.0 // km/h
	}

	Atmosphere
	{
		Height      60		// km
		Greenhouse  33		// degrees K
		Pressure    1.0		// atm
		Density     1.2929	// kg/m^3
		Adiabat     0.28
		Model      "Earth"
		Bright      10.0
		Opacity     1.0
		SkyLight    3.0
		
		Composition // values in percent
		{
			N2  77.7729
			O2  20.8625
			Ar  0.9303
			H2O 0.4000
			CO2 0.0398
		}
	}

	Aurora
	{
		Height       100   // km
        TopColor    (0.5 0.5 0.5)	// multiplier
        BottomColor (0.0 1.0 0.0)	// color

		NorthLat     82    // degrees
		NorthLon    -113   // degrees
		NorthRadius  2500  // km
		NorthWidth   600   // km
		NorthRings   3     // number of rings
		NorthBright  0.3

		SouthLat    -63    // degrees
		SouthLon     138   // degrees
		SouthRadius  2000  // km
		SouthWidth   600   // km
		SouthRings   3     // number of rings
		SouthBright  0.3
	}

	NoRings     true

	Orbit
	{
		RefPlane       "Ecliptic"
		Period          0.07480422854	// years
		SemiMajorAxis   0.000031610219244232	// a.u.
		Eccentricity    0.0549
		Inclination     5.15		// deg
		MeanAnomaly     135.27		// deg
		AscendingNode   125.08		// deg
		ArgOfPericen    138.15		// deg
		//AscNodePreces   18.6		// years
		//ArgOfPeriPreces 5.997		// years
	}
}

Moon	"Moon"
{
	ParentBody     "Earth-Moon"
	Class          "Selena"

	Mass            0.012302
	Radius          1737.4
	Oblateness      0.002

	//RotationOffset  -90
	//Obliquity       5.15
	//EqAscendNode    125.08

	Albedo          0.11
	AlbedoBond      0.11
	AlbedoGeom      0.12
	Brightness      1.95

	Surface
	{
		// Surface map author: John van Vliet
		DiffMap        "Moon/Surface-JVV"
		DiffTileSize    258
		DiffTileBorder  1

		// Elevation map author: Vladimir Romanyuk "SpaceEngineer"
		BumpMap        "Moon/Bump-SE"
		BumpTileSize    258
		BumpTileBorder  1
		BumpHeight      19.851
		BumpOffset      9.058

		Hapke           1.0
	}

	NoAtmosphere    true
	NoAurora        true
	NoRings         true

	Orbit
	{
		RefPlane       "Ecliptic"
		Period          0.07480422854
		SemiMajorAxis   0.002537908496755768
		Eccentricity    0.0549
		Inclination     5.15
		MeanAnomaly     135.27
		AscendingNode   125.08
		ArgOfPericen    318.15
		//AscNodePreces   18.6		// years
		//ArgOfPeriPreces 5.997		// years
	}
}

Planet	"Mars"
{
	ParentBody     "Sol"
	Class          "Desert"

	Mass            0.10745
	Radius          3396
	Oblateness      0.0069

	RotationPeriod  24.622962
	Obliquity       26.72
	EqAscendNode    82.91
	RotationOffset  -43.995
	//PoleRA          5.54458
	//PoleDec         52.886
	//ZeroMeridian    176.753

	Color         ( 1.000 0.750 0.700 )
	Albedo          0.25
	AlbedoBond      0.25
	AlbedoGeom      0.15
	Brightness      2.0 // 1.5

	Surface
	{
		// Surface map author: Robert Skuridin "PBC"
		DiffMap        "Mars/Surface-PBC"
		DiffTileSize    258
		DiffTileBorder  1
		DiffMapAlpha   "None"

		// Elevation map author: Robert Skuridin "PBC"
		BumpMap        "Mars/Bump-PBC"
		BumpTileSize    258
		BumpTileBorder  1
		BumpHeight      29.457 // 21249 + 8208
		BumpOffset      8.208

		Hapke           0.7
		SpotBright      8.0		// zero phase glare bright
		SpotWidth       0.05	// zero phase glare width
	}

	Clouds
	{
		// Clouds map author: Sean Young "HarbingerDawn"
		DiffMap        "Mars/Clouds-water-HD"
		DiffTileSize    258
		DiffTileBorder  1
		ModulateColor  (1.0 1.0 1.0 0.2)
		Hapke           0.2
		SpotBright      0.0
		Height          22
		Velocity        0.0 // km/h
	}

	Clouds
	{
		// Clouds map author: Sean Young "HarbingerDawn"
		DiffMap        "Mars/Clouds-dust-HD"
		DiffTileSize    258
		DiffTileBorder  1
		ModulateColor  (0.50 0.42 0.27 1.0)
		Hapke           0.2
		SpotBright      0.0
		Height          15
		Velocity        0.0 // km/h
	}

	Atmosphere
	{
		Height      132		// km
		Greenhouse  5		// degrees K
		Pressure    0.012	// 0.012 atm at lowest surface point
		Density     0.01	// kg/m^3
		Model      "Mars"
		Bright      4.0
		Opacity     1.0
		SkyLight    2.0
		
		Composition // values in percent
		{
			CO2 95.97
			Ar  1.93
			N2  1.89
			O2  0.146
			CO  0.0557
		}
	}

	NoAurora    true
	NoRings     true

	Orbit
	{
		RefPlane       "Ecliptic"
		Period          1.8809
		SemiMajorAxis   1.5237
		Eccentricity    0.0934
		Inclination     1.8506
		AscendingNode   49.479
		LongOfPericen   336.041
		MeanLongitude   355.453
	}
}

DwarfMoon	"Phobos"
{
	ParentBody     "Mars"
	Class          "Asteroid"

	// min radius = 7.952442
	// max radius = 10.4
	// avg radius = 9.3772
	Radius          9.3772 //13
	Mass            1.79E-9

	Albedo          0.02
	AlbedoBond      0.02
	AlbedoGeom      0.07
	Brightness      1.5

	Surface
	{
		// Surface map author: Robert Skuridin "PBC"
		DiffMap        "Phobos/Surface-PBC"
		DiffTileSize    258
		DiffTileBorder  1

		// Elevation map author: Vladimir Romanyuk "SpaceEngineer"
		BumpMap        "Phobos/Bump-SE"
		BumpTileSize    258
		BumpTileBorder  1
		BumpHeight      3.126636 //  amplitude = 3.126636
		BumpOffset      1.332861

		Hapke           1.0
	}

	Orbit
	{
		RefPlane       "Equator"
		Period          0.0008731466408
		SemiMajorAxis   6.268181817e-005
		Eccentricity    0.0151
		Inclination     1.075
		AscendingNode   128.694
		ArgOfPericen    213.804
		MeanAnomaly     191.771
	}
}

DwarfMoon	"Deimos"
{
	ParentBody     "Mars"
	Class          "Asteroid"

	// min radius = 5.865657
	// max radius = 7.9
	// avg radius = 6.8
	Radius          6.8 // 7.9
	Mass            2.4776E-9

	Albedo          0.02
	AlbedoBond      0.02
	AlbedoGeom      0.08
	Brightness      2.5

	Surface
	{
		// Surface map author: Robert Skuridin "PBC"
		DiffMap        "Deimos/Surface-PBC"
		DiffTileSize    258
		DiffTileBorder  1

		// Elevation map author: Vladimir Romanyuk "SpaceEngineer"
		BumpMap        "Deimos/Bump-SE"
		BumpTileSize    258
		BumpTileBorder  1
		BumpHeight      2.034343 //  amplitude = 2.034343
		BumpOffset      0.934343

		Hapke           1.0
	}

	Orbit
	{
		RefPlane       "Equator"
		Period          0.003456448899
		SemiMajorAxis   0.0001568395722
		Eccentricity    0.00033
		Inclination     1.793
		AscendingNode   25.229
		ArgOfPericen    208.213
		MeanAnomaly     344.128
	}
}

DwarfPlanet	"Ceres/(1) Ceres"
{
	ParentBody     "Sol"
	Class          "Selena"

	Mass            0.0001579

	Radius          470.925 // elevation map is relative to sphere
	Oblateness      0.0     // elevation map is relative to sphere

	RadiusInfo      487.5   // radius value for interface
	OblatenessInfo  0.068	// oblateness value for interface

	RotationPeriod  9.075
	RotationOffset  339.85
	Obliquity       11
	EqAscendNode    29

	Albedo          0.14 // fake
	AlbedoBond      0.14 // fake
	AlbedoGeom      0.09
	Brightness      1.4 // 2.4
	AbsMagn         3.34
	SlopeParam      0.12

	Surface
	{
		// Surface map author: Sean Young "HarbingerDawn"
		DiffMap        "Ceres/Surface-HD"
		DiffTileSize    258
		DiffTileBorder  1
		DiffMapAlpha   "Ice"
		Hapke           0.5

		// Elevation map author: Vladimir Romanyuk "SpaceEngineer"
		BumpMap        "Ceres/Bump-SE"
		BumpTileSize    258
		BumpTileBorder  1
		BumpHeight      16 // 33.15
		BumpOffset      8  // 16.575
	}

	Orbit
	{
		RefPlane       "Ecliptic"
		Epoch           2456401
		MeanMotion      0.214021
		SemiMajorAxis   2.76799
		Eccentricity    0.0761669
		Inclination     10.5942
		AscendingNode   80.3301
		ArgOfPericenter 72.1671
		MeanAnomaly     327.854
	}
}

Asteroid	"Pallas/(2) Pallas"
{
	ParentBody     "Sol"

	Mass            0.0000345
	Radius          263

	RotationPeriod  7.81323264
	RotationOffset  352.77
	Obliquity       102
	EqAscendNode    162

	Albedo          0.22 // fake
	AlbedoBond      0.22 // fake
	AlbedoGeom      0.159
	AbsMagn         4.13
	SlopeParam      0.11

	Orbit
	{
		RefPlane       "Ecliptic"
		Epoch           2456401
		MeanMotion      0.213555
		SemiMajorAxis   2.77202
		Eccentricity    0.2315
		Inclination     34.8368
		AscendingNode   173.12
		ArgOfPericenter 309.954
		MeanAnomaly     310.045
	}
}

Asteroid	"Juno/(3) Juno"
{
	ParentBody     "Sol"

	Mass            4.722e-6
	Radius          130

	RotationPeriod  7.811
	Obliquity       56
	EqAscendNode    196

	Albedo          0.29 // fake
	AlbedoBond      0.29 // fake
	AlbedoGeom      0.238
	AbsMagn         5.33
	SlopeParam      0.32

	Orbit
	{
		RefPlane       "Ecliptic"
		Epoch           2456401
		MeanMotion      0.225818
		SemiMajorAxis   2.67073
		Eccentricity    0.255305
		Inclination     12.9794
		AscendingNode   169.883
		ArgOfPericenter 248.31
		MeanAnomaly     257.639
	}
}

Asteroid	"Vesta/(4) Vesta"
{
	ParentBody     "Sol"

	Mass            4.337e-5

	Radius          211.946	// elevation map is relative to sphere
	Oblateness      0		// elevation map is relative to sphere

	RadiusInfo      285		// radius value for interface
	OblatenessInfo  0.19649	// oblateness value for interface

	RotationPeriod  5.342
	RotationOffset  325.77
	Obliquity       40
	EqAscendNode    91

	Albedo          0.18 // arXiv:1310.4165v1 [astro-ph.EP]
	AlbedoBond      0.18 // arXiv:1310.4165v1 [astro-ph.EP]
	AlbedoGeom      0.38 // arXiv:1310.4165v1 [astro-ph.EP]
	AbsMagn         3.2
	SlopeParam      0.32
	Brightness      2.0

	Surface
	{
		// Surface map author: Sean Young "HarbingerDawn"
		DiffMap        "Vesta/Surface-HD"
		DiffTileSize	258
		DiffTileBorder	1

		// Elevation map author: Vladimir Romanyuk "SpaceEngineer"
		// 16 bit elevation map
		BumpMap        "Vesta/Bump-SE"
		BumpTileSize	258
		BumpTileBorder	1
		BumpHeight      81.064
		BumpOffset      0

		Hapke           1.0
	}

	Orbit
	{
		RefPlane       "Ecliptic"
		Epoch           2456401
		MeanMotion      0.271432
		SemiMajorAxis   2.36245
		Eccentricity    0.0882571
		Inclination     7.13991
		AscendingNode   103.85
		ArgOfPericenter 150.94
		MeanAnomaly     218.172
	}
}

Asteroid	"Hygiea/(10) Hygiea"
{
	ParentBody     "Sol"
	Radius          431
	Mass            1.45124e-5
	RotationPeriod  27.623
	Albedo          0.01 // fake
	AlbedoBond      0.01 // fake
	Albedo          0.0717
	AbsMagn         5.43
	SlopeParam      0.15
	Orbit
	{
		RefPlane       "Ecliptic"
		Epoch           2456401
		MeanMotion      0.177388
		SemiMajorAxis   3.13703
		Eccentricity    0.116109
		Inclination     3.8419
		AscendingNode   283.418
		ArgOfPericenter 312.758
		MeanAnomaly     121.957
	}
}

Planet	"Jupiter"
{
	ParentBody     "Sol"
	Class          "GasGiant"

	Mass            317.832
	Radius          71492
	Oblateness      0.06487

	RotationPeriod  9.92425
	RotationOffset  305.4
	Obliquity       2.222461
	EqAscendNode   -22.203

	Albedo          0.343 
	AlbedoBond      0.343 
	AlbedoGeom      0.52
	Brightness      2.0

	Surface
	{
		// Surface map author: John van Vliet
		DiffMap        "Jupiter/Surface-JVV"
		DiffTileSize    256
		DiffTileBorder  0
		DayAmbient      0
	}

	NoClouds true

	Atmosphere
	{
		Height   300	// km
		Model   "Jupiter"
		Bright   2.0
		Opacity  0.0
		SkyLight 0.0
		
		Composition // values in percent
		{
			H2   89.8
			He   10.2
			CH4  0.3
			NH3  0.026
			C2H6 0.0006
		}
	}

	Aurora
	{
		Height       1000  // km
		TopColor    (1.5 0.0 0.8)
		BottomColor (1.0 0.1 0.0)

		NorthLat     90    // degrees
		NorthLon     0     // degrees
		NorthRadius  15000 // km
		NorthWidth   5000  // km
		NorthRings   3     // number of rings
		NorthBright  1.0

		SouthLat    -90    // degrees
		SouthLon     0     // degrees
		SouthRadius  16000 // km
		SouthWidth   5000  // km
		SouthRings   3     // number of rings
		SouthBright  1.0
	}

	Rings
	{
		Texture         "Jupiter-rings.*"
		InnerRadius     102200
		OuterRadius     227000
		FrontBright     0.03
		BackBright      1.0
		Density         0.03
		Brightness      2.0
	}

	Orbit
	{
		RefPlane       "Ecliptic"
		Period          11.8622
		SemiMajorAxis   5.2034
		Eccentricity    0.0484
		Inclination     1.3053
		AscendingNode   100.556
		LongOfPericen   14.7539
		MeanLongitude   34.404
	}
}

DwarfMoon	"Metis/Jupiter XVI"
{
	ParentBody     "Jupiter"
	Class          "Asteroid"

	Radius          21.5
	Mass            6.0E-9

	Albedo          0.061
	Brightness      0.6

	Orbit
	{
		Period          0.0008070808904
		SemiMajorAxis   0.0008554745988
		Eccentricity    0.0012
		Inclination     0.019
		AscendingNode   263.953
		ArgOfPericen    274.588
		MeanAnomaly     7.649
	}
}


DwarfMoon	"Adrastea/Jupiter XV"
{
	ParentBody     "Jupiter"
	Class          "Asteroid"

	Radius          8.2
	Mass            3.3E-10

	Albedo          0.1
	Brightness      1.2

	Orbit
	{
		Period          0.0008166088146
		SemiMajorAxis   0.0008621657753
		Eccentricity    0.0018
		Inclination     0.054
		AscendingNode   36.236
		ArgOfPericen    213.656
		MeanAnomaly     344.875
	}
}

DwarfMoon	"Amalthea/Jupiter V"
{
	ParentBody     "Jupiter"
	Class          "Asteroid"

	Mass            3.515E-7
	Radius          83.5 // 51.861430 //134

	Color         ( 0.514 0.322 0.251 )
	Albedo          0.06
	Brightness      0.7

	Surface
	{
		// Surface map author: Vladimir Romanyuk "SpaceEngineer"
		DiffMap        "Amalthea/Surface-SE"
		DiffTileSize    130
		DiffTileBorder  1

		// Elevation map author: Vladimir Romanyuk "SpaceEngineer"
		BumpMap        "Amalthea/Bump-SE"
		BumpTileSize    34
		BumpTileBorder  1
		BumpHeight      96.496507
		BumpOffset      31.63857
	}

	Orbit
	{
		Epoch           2452583.763
		Period          0.001363968895
		SemiMajorAxis   0.001216542647
		Eccentricity    0.0045045
		Inclination     0.384285
		AscendingNode   220.288958
		ArgOfPericen    301.622765
		MeanAnomaly     315.352094
	}
}

DwarfMoon	"Thebe/Jupiter XIV"
{
	ParentBody      "Jupiter"

	Radius          49.3
	Mass            7.2E-8

	Albedo          0.047
	Brightness      1.0

	Orbit
	{
		Period          0.001846719793
		SemiMajorAxis   0.00148328877
		Eccentricity    0.0177
		Inclination     1.075
		AscendingNode   0.415
		ArgOfPericen    28.001
		MeanAnomaly     181.09
	}
}

Moon	"Io"
{
	ParentBody     "Jupiter"
	Class          "Selena"

	Mass            0.01495
	Radius          1821.6

	Color         ( 0.569 0.471 0.388 )
	Albedo          0.61
	Brightness      3.0

	Surface
	{
		// Surface map author: Robert Skuridin "PBC"
		DiffMap        "Io/Surface-PBC"
		DiffTileSize    258
		DiffTileBorder  1
		DiffMapAlpha   "None"

		// Elevation map author: Vladimir Romanyuk "SpaceEngineer"
		BumpMap        "Io/Bump-fake-SE"
		BumpTileSize    258
		BumpTileBorder  1
		BumpHeight      6.0
		BumpOffset      3.0

		// Lights map author: Vladimir Romanyuk "SpaceEngineer"
		GlowMap        "Io/Lights-SE"
		GlowTileSize    258
		GlowTileBorder  1
		GlowMode       "Permanent"

		Hapke           1.0
	}

	NoLava true

	Atmosphere
	{
		Height      70		// km
		Pressure    1e-9
		Model      "Sun"
		Bright      1.0
		Opacity     0.0
		SkyLight    0.0
		
		Composition // values in percent
		{
			SO2  90.66
			SO   9.066
			NaCl 0.274
		}
	}

	Orbit
	{
		Epoch          2443000
		Period         0.004843739305
		SemiMajorAxis  0.002818181818
		Eccentricity   0.0041
		Inclination    0.04
		AscendingNode  312.981
		LongOfPericen  97.735
		MeanLongitude  106.724
	}
}

Moon	"Europa"
{
	ParentBody     "Jupiter"
	Class          "IceWorld"

	Mass            0.008035
	Radius          1560.8

	Color         ( 0.745 0.718 0.655 )
	Albedo          0.64
	Brightness      2.4

	Surface
	{
		// Surface map author: John van Vliet
		DiffMap        "Europa/Surface-JVV"
		DiffTileSize    258
		DiffTileBorder  1
		DiffMapAlpha   "Ice"

		// Elevation map author: Vladimir Romanyuk "SpaceEngineer"
		BumpMap        "Europa/Bump-fake-SE"
		BumpTileSize    258
		BumpTileBorder  1
		BumpHeight		2.0
		BumpOffset		1.0

		Hapke           0.5
	}

	NoAtmosphere    true

	Orbit
	{
		Epoch          2443000
		Period         0.009724533474
		SemiMajorAxis  0.004484625668
		Eccentricity   0.0101
		Inclination    0.47
		AscendingNode  101.087
		LongOfPericen  155.512
		MeanLongitude  176.377
	}
}

Moon	"Ganymede"
{
	ParentBody     "Jupiter"
	Class          "IceWorld"

	Mass            0.0248
	Radius          2631.2

	Color         ( 0.529 0.478 0.424 )
	Albedo          0.42
	Brightness      2.0

	Surface
	{
		// Surface map author: Robert Skuridin "PBC"
		DiffMap        "Ganymede/Surface-PBC"
		DiffTileSize    258
		DiffTileBorder  1
		DiffMapAlpha   "None"

		// Elevation map author: Vladimir Romanyuk "SpaceEngineer"
		BumpMap        "Ganymede/Bump-fake-SE"
		BumpTileSize    258
		BumpTileBorder  1
		BumpHeight      4.0
		BumpOffset      2.0

		Hapke           1.0
	}

	NoAtmosphere    true

	Orbit
	{
		Epoch          2443000
		Period         0.01958851688
		SemiMajorAxis  0.007152406416
		Eccentricity   0.0015
		Inclination    0.195
		AscendingNode  119.841
		LongOfPericen  188.831
		MeanLongitude  121.206
	}
}

Moon	"Callisto"
{
	ParentBody     "Jupiter"
	Class          "IceWorld"

	Mass            0.01801155
	Radius          2410.3

	Color         ( 0.369 0.322 0.271 )
	Albedo          0.2
	Brightness      2.0

	Surface
	{
		// Surface map author: John van Vliet
		DiffMap        "Callisto/Surface-JVV"
		DiffTileSize    258
		DiffTileBorder  1
		DiffMapAlpha   "None"

		// Elevation map author: Vladimir Romanyuk "SpaceEngineer"
		BumpMap        "Callisto/Bump-fake-SE"
		BumpTileSize    258
		BumpTileBorder  1
		BumpHeight      4.0
		BumpOffset      2.0

		Hapke           1.0
	}

	Atmosphere
	{
		Height      36
		Pressure    2e-10
		Greenhouse  0
		Model      "Sun"
		Bright      0.4
		
		Composition // values in percent
		{
			O2  96.25
			CO2 3.75
		}
	}

	Orbit
	{
		Epoch          2443000
		Period         0.04569301685
		SemiMajorAxis  0.01258689839
		Eccentricity   0.007
		Inclination    0.281
		AscendingNode  323.265
		LongOfPericen  335.933
		MeanLongitude  85.091
	}
}

Planet	"Saturn"
{
	ParentBody     "Sol"
	Class          "GasGiant"

	Mass            95.162
	Radius          60268
	Oblateness      0.09796

	RotationPeriod  10.65622
	RotationOffset  358.922
	Obliquity       28.049
	EqAscendNode    169.53

	Color         ( 1.000 1.000 0.850 )
	Albedo          0.342
	AlbedoBond      0.342
	AlbedoGeom      0.47
	Brightness      1.8

	Surface
	{
		// Surface map author: Bjorn Jonsson
		DiffMap        "Saturn/Surface-BJ"
		DiffTileSize    256
		DiffTileBorder  0
		DayAmbient      0
	}

	NoClouds     true

	Atmosphere
	{
		Height   250		// km
		Model   "Earth"
		Bright   5.0
		Opacity  0.2
		SkyLight 0.002
		
		Composition // values in percent
		{
			H2   96.3
			He   3.25
			CH4  0.45
			NH3  0.0125
			C2H6 0.0007
		}
	}

	Aurora
	{
		Height       1000   // km
		TopColor    (1.5 0.0 0.8)
		BottomColor (1.0 0.1 0.0)

		NorthLat     90    // degrees
		NorthLon     0     // degrees
		NorthRadius  15000 // km
		NorthWidth   5000  // km
		NorthRings   3     // number of rings
		NorthBright  1.0

		SouthLat    -90    // degrees
		SouthLon     0     // degrees
		SouthRadius  16000 // km
		SouthWidth   5000  // km
		SouthRings   3     // number of rings
		SouthBright  1.0
	}

	Rings
	{
		// Rings texture author: Vladimir Romanyuk "SpaceEngineer"
		Texture        "Saturn-rings.*"
		InnerRadius     60284
		OuterRadius     356993
		FrontBright     1.0
		BackBright      5.0
		Brightness      1.5
	}

	Orbit
	{
		RefPlane       "Ecliptic"
		Period          29.4577
		SemiMajorAxis   9.5371
		Eccentricity    0.0542
		Inclination     2.4845
		AscendingNode   113.715
		LongOfPericen   92.432
		MeanLongitude   49.944
	}
}

DwarfMoon	"Prometheus/Saturn XVI"
{
	ParentBody     "Saturn"
	Class          "Asteroid"

	Mass            2.68E-8
	Radius          74

	RotationOffset  236

	Albedo          0.6
	Brightness      3.0

	Surface
	{
		//DiffMap    "Prometheus.*"
		SurfStyle   0.0
		Hapke       1.0
	}

	Orbit
	{
		Period         0.001678300043
		SemiMajorAxis  0.0009314839571
		Eccentricity   0.0023
		Inclination    0
		LongOfPericen  32.879
		MeanAnomaly    22.695
	}
}

DwarfMoon	"Pandora/Saturn XVII"
{
	ParentBody     "Saturn"
	Class          "Asteroid"

	Mass            2.34E-8
	Radius          57

	RotationOffset  117

	Albedo          0.5
	Brightness      6.0

	Surface
	{
		//DiffMap    "Pandora.*"
		SurfStyle   0.0
		Hapke       1.0
	}

	Orbit
	{
		Period         0.001721608292
		SemiMajorAxis  0.0009471925132
		Eccentricity   0.0044
		Inclination    0
		LongOfPericen  234.458
		MeanAnomaly    62.483
	}
}

DwarfMoon	"Epimetheus/Saturn XI"
{
	ParentBody     "Saturn"
	Class          "Asteroid"

	Mass            8.87E-8
	Radius          72

	RotationOffset  249

	Albedo          0.5
	Brightness      3.2

	Surface
	{
		//DiffMap    "Epimetheus.*"
		SurfStyle   0.0
		Hapke       1.0
	}

	Orbit
	{
		Period         0.00190172439
		SemiMajorAxis  0.001012179144
		Eccentricity   0.0205
		Inclination    0.337
		AscendingNode  149.88
		ArgOfPericen   86.905
		MeanAnomaly    192.354
	}
}

DwarfMoon	"Janus/Saturn X"
{
	ParentBody     "Saturn"
	Class          "Asteroid"

	Mass            3.18E-7
	Radius          96

	RotationOffset  64

	Albedo          0.6
	Brightness      3.5

	Surface
	{
		//DiffMap    "Janus.*"
		SurfStyle   0.0
		Hapke       1.0
	}

	Orbit
	{
		Period         0.00190172439
		SemiMajorAxis  0.001012513369
		Eccentricity   0.0073
		Inclination    0.168
		AscendingNode  120.557
		ArgOfPericen   43.066
		MeanAnomaly    79.973
	}
}

Moon	"Mimas/Saturn I"
{
	ParentBody     "Saturn"
	Class          "IceWorld"

	Mass            6.19E-6
	Radius          202.25
	Oblateness      0.06

	Color         ( 1.000 0.840 0.773 )
	Albedo          0.75
	Brightness      2.5 // 4.0

	Surface
	{
		// Surface map author: Robert Skuridin "PBC"
		DiffMap        "Mimas/Surface-PBC"
		DiffTileSize    258
		DiffTileBorder  1

		// Elevation map author: Vladimir Romanyuk "SpaceEngineer"
		BumpMap        "Mimas/Bump-fake-SE"
		BumpTileSize    258
		BumpTileBorder  1
		BumpHeight      10.2914
		BumpOffset		8.0

		Hapke           0.7
	}

	Orbit
	{
		Epoch          2451179.5
		Period         0.002580265369
		SemiMajorAxis  0.001240106952
		Eccentricity   0.0206
		Inclination    1.566
		ArgOfPericen   332.864
		AscendingNode  177.459
		MeanAnomaly    100.173
	}
}

Moon	"Enceladus/Saturn II"
{
	ParentBody     "Saturn"
	Class          "IceWorld"

	Mass            1.841E-5
	Radius          249.4

	Albedo          0.99
	AlbedoBond      0.99
	AlbedoGeom      1.4
	Brightness      4.0 // 4.4

	Surface
	{
		// Surface map author: Robert Skuridin "PBC"
		DiffMap        "Enceladus/Surface-PBC"
		DiffTileSize    258
		DiffTileBorder  1
		Hapke           1.0
	}

	Orbit
	{
		Epoch          2451179.5
		Period         0.003751532545
		SemiMajorAxis  0.001591042781
		Eccentricity   0.0001
		Inclination    0.01
		ArgOfPericen   334.689
		AscendingNode  137.077
		MeanAnomaly    162.044
	}
}

Moon	"Tethys/Saturn III"
{
	ParentBody     "Saturn"
	Class          "IceWorld"

	Mass            1.037E-4
	Radius          529.9

	Albedo          0.9
	Brightness      2.5 // 5.3

	Surface
	{
		// Surface map author: Robert Skuridin "PBC"
		DiffMap        "Tethys/Surface-PBC"
		DiffTileSize    258
		DiffTileBorder  1
		Hapke           0.7
	}

	Orbit
	{
		Epoch          2451179.5
		Period         0.005168630569
		SemiMajorAxis  0.001969652406
		Eccentricity   0.0001
		Inclination    0.168
		ArgOfPericen   149.166
		AscendingNode  149.174
		MeanAnomaly    28.755
	}
}

Moon	"Dione/Saturn IV"
{
	ParentBody     "Saturn"
	Class          "IceWorld"

	Mass            1.84E-4
	Radius          559

	Albedo          0.7
	Brightness      2.5 // 3.5

	Surface
	{
		// Surface map author: Robert Skuridin "PBC"
		DiffMap        "Dione/Surface-PBC"
		DiffTileSize    258
		DiffTileBorder  1
		Hapke           0.7
	}

	Orbit
	{
		Epoch          2451179.5
		Period         0.007493424911
		SemiMajorAxis  0.002522727272
		Eccentricity   0.0002
		Inclination    0.002
		AscendingNode  57.741
		ArgOfPericen   173.999
		MeanAnomaly    109.204
	}
}

Moon	"Rhea/Saturn V"
{
	ParentBody     "Saturn"
	Class          "IceWorld"

	Mass            3.85E-4
	Radius          764

	Albedo          0.7
	Brightness      2.5 // 3.6

	Surface
	{
		// Surface map author: Robert Skuridin "PBC"
		DiffMap        "Rhea/Surface-PBC"
		DiffTileSize    258
		DiffTileBorder  1
		Hapke           0.7
	}

	Orbit
	{
		Epoch          2451179.5
		Period         0.01236850506
		SemiMajorAxis  0.003522994652
		Eccentricity   0.0009
		Inclination    0.327
		AscendingNode  1.095
		ArgOfPericen   205.869
		MeanAnomaly    109.204
	}
}

Moon	"Titan/Saturn VI"
{
	ParentBody     "Saturn"
	Class          "Titan"

	Mass            0.02176
	Radius          2574.91

	Color         ( 0.960 0.805 0.461 )
	Albedo          0.21
	Brightness      1.45

	NoLife          true

	Surface
	{
		// Surface map author: Vladimir Romanyuk "SpaceEngineer"
		DiffMap        "Titan/Surface-SE"
		DiffTileSize    258
		DiffTileBorder  1
		DiffMapAlpha   "Water"

		// Elevation map author: Vladimir Romanyuk "SpaceEngineer"
		BumpMap        "Titan/Bump-fake-SE"
		BumpTileSize    258
		BumpTileBorder  1
		BumpHeight      2.5
		DiffMapAlpha   "Water"

		OceanStyle      0.8
	}

	Ocean
	{
		Height    0.25
	}

	Clouds
	{
		DiffMap        "Titan-clouds.*"
		DiffMapAlpha   "None"
		DayAmbient      3.0
		Height          10
	}

	Clouds
	{
		DiffMap        "Titan-clouds.*"
		DiffMapAlpha   "None"
		DayAmbient      3.0
		Height          30
	}

	Clouds
	{
		DiffMap        "Titan-clouds.*"
		DiffMapAlpha   "None"
		DayAmbient      3.0
		Height          50
	}

	Atmosphere
	{
		Height      200		// km
		Greenhouse  12		// degrees K
		Pressure    1.5		// atm
		Density     4.8		// kg/m^3
		Model      "Titan"
		//Bright      10.0
		//Opacity     1.0
		//SkyLight    3.0
		
		Composition // values in percent
		{
			N2  98.4
			CH4 1.4
			H2  0.2
		}
	}

	NoAurora    true

	Orbit
	{
		Epoch          2451179.5
		Period         0.04365711574
		SemiMajorAxis  0.008167446523
		Eccentricity   0.0288
		Inclination    1.634
		AscendingNode  44.046
		ArgOfPericen   172.749
		MeanAnomaly    192.132
	}
}

DwarfMoon	"Hyperion/Saturn VII"
{
	ParentBody     "Saturn"
	Class          "Asteroid"

	Mass            9.54E-7

	// min radius = 96.8322
	// max radius = 187.7578
	// avg radius = 171
	Radius          171

	RotationPeriod  120
	Obliquity       61
	EqAscendNode    145

	Albedo          0.3
	Brightness      2.5

	Surface
	{
		// Surface map author: John van Vliet
		DiffMap        "Hyperion/Surface-JVV"
		DiffTileSize    258
		DiffTileBorder  1

		// Elevation map author: Vladimir Romanyuk "SpaceEngineer"
		BumpMap        "Hyperion/Bump-fake-SE"
		BumpTileSize    258
		BumpTileBorder  1
		BumpHeight      90.9256
		BumpOffset      74.1678

		Hapke           1.0
	}

	Orbit
	{
		Epoch          2451179.5
		Period         0.05825342471
		SemiMajorAxis  0.009900401068
		Eccentricity   0.0175
		Inclination    0.43
		AscendingNode  273.863
		ArgOfPericen   262.058
		MeanAnomaly    59.956
	}
}

Moon	"Iapetus/Saturn VIII"
{
	ParentBody     "Saturn"
	Class          "IceWorld"

	Mass            3.35E-4
	Radius          718

	Obliquity       15.5
	EqAscendNode    75.583

	Albedo          0.2
	Brightness      2.5

	Surface
	{
		// Surface map author: Robert Skuridin "PBC"
		DiffMap        "Iapetus/Surface-PBC"
		DiffTileSize    258
		DiffTileBorder  1

		// Elevation map author: Tristan Audam "Voekoevaka"
		BumpMap        "Iapetus/Bump-fake-VO"
		BumpTileSize    258
		BumpTileBorder  1
		BumpHeight      24.0

		Hapke           1.0
	}

	Orbit
	{
		Epoch          2451179.5
		Period         0.2171988423
		SemiMajorAxis  0.02380548128
		Eccentricity   0.0284
		Inclination    7.57
		AscendingNode  75.583
		ArgOfPericen   275.85
		MeanAnomaly    350.306
	}
}

DwarfMoon	"Phoebe/Saturn IX"
{
	ParentBody     "Saturn"
	Class          "IceWorld"

	Mass            1.39E-6
	Radius          110

	RotationPeriod  9.282
	RotationOffset  320.3
	Obliquity       17.4
	EqAscendNode    259.9

	Albedo          0.05
	Brightness      0.5

	Surface
	{
		//DiffMap    "Phoebe.*"
		Hapke       1.0
	}

	Orbit
	{
		Epoch          2451179.5
		Period         1.500949233
		SemiMajorAxis  0.08633363635
		Eccentricity   0.1644
		Inclination    174.751
		AscendingNode  237.143
		ArgOfPericen   337.537
		MeanAnomaly    174.569
	}
}

Planet	"Uranus"
{
	ParentBody     "Sol"
	Class          "IceGiant"

	Mass            14.536
	Radius          25559
	Oblateness      0.02293

	RotationPeriod  17.24
	RotationOffset  331.18
	Obliquity       97.81
	EqAscendNode    167.76

	Color         ( 0.750 0.850 1.000 )
	Albedo          0.30
	AlbedoBond      0.30
	AlbedoGeom      0.51
	Brightness      1.2

	Surface
	{
		DiffMap        "Uranus.*"
	}

	NoClouds    true

	Atmosphere
	{
		Height   300		// km
		Model   "Earth"
		Bright   2.0
		Opacity  0.2
		SkyLight 0.002
		
		Composition // values in percent
		{
			H2  82.5
			He  15.2
			CH4 2.3
		}
	}

	Aurora
	{
		Height       600   // km
		TopColor    (1.5 0.0 0.8)
		BottomColor (1.0 0.1 0.0)

		NorthLat     90    // degrees
		NorthLon     0     // degrees
		NorthRadius  5000  // km
		NorthWidth   1500  // km
		NorthRings   3     // number of rings
		NorthBright  0.3

		SouthLat    -90    // degrees
		SouthLon     0     // degrees
		SouthRadius  5000  // km
		SouthWidth   1500  // km
		SouthRings   3     // number of rings
		SouthBright  0.3
	}

	Rings
	{
		Texture        "Uranus-rings.*"
		InnerRadius     39501.5
		OuterRadius     53514.4
	}

	Orbit
	{
		RefPlane       "Ecliptic"
		Period          84.0139
		SemiMajorAxis   19.1913
		Eccentricity    0.0472
		Inclination     0.7699
		AscendingNode   74.23
		LongOfPericen   170.964
		MeanLongitude   313.232
	}
}

Moon	"Miranda/Uranus V"
{
	ParentBody     "Uranus"
	Class          "IceWorld"

	Mass            1.1E-5
	Radius          235.8

	Albedo          0.32
	Brightness      1.2

	Surface
	{
		DiffMap        "Miranda/Surface"
		DiffTileSize    258
		DiffTileBorder  1

		// Elevation map author: Tristan Audam "Voekoevaka"
		BumpMap        "Miranda/Bump-fake-VO"
		BumpTileSize    258
		BumpTileBorder  1
		BumpHeight      15.0
	}

	Orbit
	{
		Period         0.003868665778
		SemiMajorAxis  0.0008676470587
		Eccentricity   0.0027
		Inclination    4.22
		MeanAnomaly    120
	}
}

Moon	"Ariel/Uranus I"
{
	ParentBody     "Uranus"
	Class          "IceWorld"

	Mass            2.26E-4
	Radius          578.9

	Albedo          0.39
	Brightness      2.25

	Surface
	{
		DiffMap    "Ariel.*"
	}

	Orbit
	{
		Period         0.006899531325
		SemiMajorAxis  0.001278074866
		Eccentricity   0.0034
		Inclination    0.31
		MeanAnomaly    56
	}
}

Moon	"Umbriel/Uranus II"
{
	ParentBody     "Uranus"
	Class          "IceWorld"

	Mass            1.96E-4
	Radius          584.7

	Albedo          0.21
	Brightness      1.25

	Surface
	{
		DiffMap    "Umbriel.*"
	}

	Orbit
	{
		Period         0.01134589596
		SemiMajorAxis  0.001778074866
		Eccentricity   0.005
		Inclination    0.36
		MeanAnomaly    280
	}
}

Moon	"Titania/Uranus III"
{
	ParentBody     "Uranus"
	Class          "IceWorld"

	Mass            5.91E-4
	Radius          788.9

	Albedo          0.27
	Brightness      1.35

	Surface
	{
		DiffMap        "Titania/Surface"
		DiffTileSize    258
		DiffTileBorder  1
		Hapke           1.0
	}

	Orbit
	{
		Period         0.02383623798
		SemiMajorAxis  0.002913101604
		Eccentricity   0.0022
		Inclination    0.1
		MeanAnomaly    30
	}
}

Moon	"Oberon/Uranus IV"
{
	ParentBody     "Uranus"
	Class          "IceWorld"

	Mass            5.04E-4
	Radius          761.4

	Albedo          0.23
	Brightness      1.45

	Surface
	{
		DiffMap    "Oberon.*"
	}

	Orbit
	{
		Period         0.03686047231
		SemiMajorAxis  0.003901069518
		Eccentricity   0.0008
		Inclination    0.1
		MeanAnomaly    150
	}
}

Planet	"Neptune"
{
	ParentBody     "Sol"
	Class          "IceGiant"

	Mass            17.147
	Radius          24766
	Oblateness      0.01708

	RotationPeriod  16.11
	RotationOffset  228.65
	Obliquity       28.03
	EqAscendNode    49.235

	Color         ( 0.750 0.750 1.000 )
	Albedo          0.29 
	AlbedoBond      0.29 
	AlbedoGeom      0.41
	Brightness      1.07

	Surface
	{
		DiffMap        "Neptune.*"
	}

	NoClouds    true

	Atmosphere
	{
		Height   300		// km
		Model   "Neptune"
		Bright   5.0
		Opacity  0.2
		SkyLight 0.2
		
		Composition // values in percent
		{
			H2   80
			He   19
			CH4  1.5
			C2H6 0.00015
		}
	}

	Aurora
	{
		Height       600   // km
		TopColor    (1.5 0.0 0.8)
		BottomColor (1.0 0.1 0.0)

		NorthLat     90    // degrees
		NorthLon     0     // degrees
		NorthRadius  5000  // km
		NorthWidth   1500  // km
		NorthRings   3     // number of rings
		NorthBright  0.3

		SouthLat    -90    // degrees
		SouthLon     0     // degrees
		SouthRadius  5000  // km
		SouthWidth   1500  // km
		SouthRings   3     // number of rings
		SouthBright  0.3
	}

	Rings
	{
		Texture        "Neptune-rings.*"
		InnerRadius     50700.75
		OuterRadius     65396.25
	}

	Orbit
	{
		RefPlane       "Ecliptic"
		Period          164.793
		SemiMajorAxis   30.069
		Eccentricity    0.0086
		Inclination     1.7692
		AscendingNode   131.722
		LongOfPericen   44.971
		MeanLongitude   304.88
	}
}

DwarfMoon	"Larissa/Neptune VII/S1981 N 1"
{
	ParentBody     "Neptune"
	Class          "Asteroid"

	Radius          98
	Mass            8.2E-7

	Albedo          0.056
	Brightness      1.6

	Surface
	{
		Hapke       1.0
	}

	Orbit
	{
		Period         0.00151859232
		SemiMajorAxis  0.000491631016
		Eccentricity   0.0014
		Inclination    0.744
		AscendingNode  324.857
		ArgOfPericen   215.004
		MeanAnomaly    157.543
	}
}

Moon	"Proteus/Neptune VIII/S1989 N 1"
{
	ParentBody     "Neptune"
	Class          "Asteroid"

	Radius          219
	Mass            8.37E-6

	Albedo          0.061
	Brightness      1.5

	Surface
	{
		//DiffMap    "Proteus.*"
		Hapke       1.0
	}

	Orbit
	{
		Epoch          2450085.5
		Period         0.003072796626
		SemiMajorAxis  0.0007864104277
		Eccentricity   0.0005
		Inclination    1.09
		AscendingNode  330.943
		ArgOfPericen   267.246
		MeanAnomaly    213.916
	}
}

Moon	"Triton/Neptune I"
{
	ParentBody     "Neptune"
	Class          "IceWorld"

	Radius          1353.4
	Mass            3.515E-3

	Albedo          0.756 // geometric
	Brightness      2.5

	Surface
	{
		// Surface map author: Alexander Kiryushenko "Dizel777"
		DiffMap        "Triton/Surface-DI"
		DiffTileSize    258
		DiffTileBorder  1
		Hapke           1.0
	}

	Clouds
	{
		Height          3
		BumpHeight      0
		Hapke           0.2
		SpotBright      2
		SpotWidth       0.15
		DayAmbient      2
		ModulateColor  (5 5 5 0.05)
		mainFreq        1.5
		mainOctaves     6
		cycloneDensity  0
		Coverage        0.02
		twistZones      1.0
		twistMagn       1.0
	}

	Atmosphere
	{
		Height      60  // km
		Pressure    1.9e-5
		Greenhouse  0
		Model      "Pluto"
		Bright      2.2
		Opacity     0.2
		SkyLight    0.01
		
		Composition // values in percent
		{
			N2  99.82
			CO  0.126
			CH4 0.053
		}
	}

	NoAurora true

	Orbit
	{
		Epoch          2447763.5
		Period         0.01609029324
		SemiMajorAxis  0.002371425708
		Eccentricity   2.285e-005
		Inclination    156.82624
		AscendingNode  147.899288
		ArgOfPericen   293.0924
		MeanAnomaly    315.726316
	}
}

Moon	"Nereid/Neptune II"
{
	ParentBody     "Neptune"
	Class          "Asteroid"

	Radius          170
	Mass            5.19E-6

	Albedo          0.155
	Brightness      2.4

	Surface
	{
		Hapke       1.0
	}

	Orbit
	{
		Epoch          2447763.5
		Period         0.986020208
		SemiMajorAxis  0.03685427807
		Eccentricity   0.7512
		Inclination    28.385
		AscendingNode  190.678
		ArgOfPericen   17.69
		MeanAnomaly    36.056
	}
}

Barycenter "Pluto-Charon"
{
	ParentBody     "Sol"
	Orbit
	{
		RefPlane       "Ecliptic"
		Period          248.54
		SemiMajorAxis   39.48168677
		Eccentricity    0.24880766
		Inclination     17.14175
		AscendingNode   110.30347
		LongOfPericen   224.06776
		MeanLongitude   238.92881
	}
}

DwarfPlanet	"Pluto"
{
	ParentBody     "Pluto-Charon"
	Class          "IceWorld"
	AsterType      "Plutino"

	Mass            2.185E-3
	Radius          1185

	RotationPeriod  153.29389971661
	Obliquity       112.783
	EqAscendNode    223.0539
    RotationOffset  45
	TidalLocked     true

	// Color          (0.580 0.435 0.333) // (0.443 0.392 0.310)
	Albedo          0.6
	AlbedoBond      0.5 
	AlbedoGeom      0.6
	Brightness      2.0
	AbsMagn        -0.8
	SlopeParam      0.15

	Surface
	{
		// Surface map author: "Snowfall"
		DiffMap        "Pluto/Surface-HD"
		DiffTileSize    258
		DiffTileBorder  1
		DiffMapAlpha   "Ice"
/*
		// Elevation map author: Sean Young "HarbingerDawn"
		BumpMap        "Pluto/Bump-FR"
		BumpTileSize    258
		BumpTileBorder  1
		BumpHeight      3.5
		BumpOffset		0.5
*/
		Hapke           1.0
	}

	Atmosphere
	{
		Height      300 // km
		Pressure    1e-5
		Greenhouse  0
		Model      "Pluto"
		Bright      2.0
		Opacity     0.15
		SkyLight    0.01
		
		Composition // values in percent
		{
			N2   99.7
			CH4  0.25
			CO   0.05
			C2H2 3e-6
			C2H4 1e-6
		}
	}

	NoAurora true

	Orbit
	{
		RefPlane       "Ecliptic"
		Period          0.01748769994
		SemiMajorAxis   0.0000151732727282
		Eccentricity    0.003484
		Inclination     112.783 //96.1680
		AscendingNode   223.0539
		ArgOfPericen    337.92
		MeanAnomaly     56.9861
	}
}

Moon	"Charon"
{
	ParentBody     "Pluto-Charon"
	Class          "IceWorld"

	Mass            2.54E-4
	Radius          593

	RotationPeriod  153.29389971661
	Obliquity       112.783
	EqAscendNode    223.0539
    RotationOffset  0
	TidalLocked     true

	// Color          (0.500 0.435 0.408) // (0.541 0.502 0.475)
	Albedo          0.35
	Brightness      2.0

	Surface
	{
		// Surface map author: "Snowfall"
		DiffMap        "Charon/Surface-HD"
		DiffTileSize    258
		DiffTileBorder  1
		DiffMapAlpha   "Ice"
/*
		// Elevation map author: Sean Young "HarbingerDawn"
		BumpMap        "Charon/Bump-FR"
		BumpTileSize    258
		BumpTileBorder  1
		BumpHeight      3.5
		BumpOffset		0.5
*/
		Hapke           1.0
	}

	NoAurora true

	Orbit
	{
		RefPlane       "Ecliptic"
		Period          0.01748769994
		SemiMajorAxis   0.0001312566845
		Eccentricity    0.003484
		Inclination     112.783
		AscendingNode   223.0539
		ArgOfPericen    157.92
		MeanAnomaly     56.9861
	}
}

DwarfMoon	"Styx"
{
	ParentBody     "Pluto-Charon"
	Class          "Asteroid"

	Radius          3
	RotationPeriod  495.1441681712142

	Albedo          0.08

	Orbit
	{
		RefPlane       "Ecliptic"
		Period          0.05530459
		SemiMajorAxis   0.00028075
		Eccentricity    0.00001
		Inclination     112.783
		AscendingNode   223.0539
		ArgOfPericen    149.649
		MeanAnomaly     336.760
	}
}

DwarfMoon	"Nix"
{
	ParentBody     "Pluto-Charon"
	Class          "Asteroid"

	Radius          20
	RotationPeriod  43.7761445326
	Obliquity       132

	Albedo          0.08

	Orbit
	{
		RefPlane       "Ecliptic"
		Period          0.068064424
		SemiMajorAxis   0.000325594
		Eccentricity    0.003
		Inclination     112.783
		AscendingNode   223.0539
		ArgOfPericen    270.877
		MeanAnomaly     260.688
	}
}

DwarfMoon	"Kerberos"
{
	ParentBody     "Pluto-Charon"
	Class          "Asteroid"

	Radius          4.5
	RotationPeriod  127.570721902

	Albedo          0.08

	Orbit
	{
		RefPlane       "Ecliptic"
		Period          0.08788689
		SemiMajorAxis   0.00039439
		Eccentricity    0.0
		Inclination     112.783	// 0.4 deg to Pluto's equator
		AscendingNode   223.0539
		ArgOfPericen    149.649
		MeanAnomaly     336.760
	}
}

DwarfMoon	"Hydra"
{
	ParentBody     "Pluto-Charon"
	Class          "Asteroid"

	Radius          19
	RotationPeriod  10.2798546073

	Albedo          0.08

	Orbit
	{
		Period         0.104588133
		SemiMajorAxis  0.000432822
		Eccentricity   0.00554
		Inclination    112.995	// 0.3 deg to Pluto's equator
		AscendingNode  223.0539
		ArgOfPericen   149.649
		MeanAnomaly    336.760
		RefPlane      "Ecliptic"
	}
}

DwarfPlanet	"Haumea/(136108) Haumea/2003 EL61"
{
	ParentBody     "Sol"
	AsterType      "Cubewano"

	Radius          869.5 // mean equatorial radius // 980 // maximum semi-axis
	Oblateness      0.427
	Mass            0.00066

	RotationPeriod  3.9155
	Obliquity       126   // used Hi'iaka's orbit Inclination
	EqAscendNode    26    // used Hi'iaka's orbit AscendingNode

	AbsMagn         0.1
	SlopeParam      0.15
	Albedo          0.7
	Brightness      1.5 // 3.0

	Orbit
	{
		RefPlane       "Ecliptic"
		Epoch           2456401
		MeanMotion      0.00348839
		SemiMajorAxis   43.0579
		Eccentricity    0.195253
		Inclination     28.1941
		AscendingNode   121.819
		ArgOfPericenter 240.678
		MeanAnomaly     206.48
	}
}

DwarfMoon	"Hi'iaka/Haumea I"
{
	ParentBody     "Haumea"
	Class          "Asteroid"

	Radius          155
	Mass            2.9962e-6

	Albedo          0.8

	Orbit
	{
		RefPlane       "Ecliptic"
		Period          0.1344861026
		SemiMajorAxis   0.0003308823529
		Eccentricity    0.05
		Inclination     126.356
		AscendingNode   26.1
		ArgOfPericen    278.6
		MeanAnomaly     16.71
	}
}

DwarfMoon	"Namaka/Haumea II"
{
	ParentBody     "Haumea"
	Class          "Asteroid"

	Radius          85
	Mass            2.9962e-7

	Albedo          0.8

	Orbit
	{
		RefPlane       "Ecliptic"
		Period          0.09336270562
		SemiMajorAxis   0.0002627005347
		Eccentricity    0.23
		Inclination     113.031
		AscendingNode   25
	}
}

DwarfPlanet	"Makemake/(136472) Makemake/2005 FY9"
{
	ParentBody     "Sol"
	AsterType      "Cubewano"

	Radius          750
	Mass            0.0005

	AbsMagn        -0.4
	SlopeParam      0.15
	Albedo          0.78
	Brightness      1.5 // 2.5

	RotationPeriod  7.771

	Orbit
	{
		RefPlane       "Ecliptic"
		Epoch           2456401
		MeanMotion      0.00320549
		SemiMajorAxis   45.5554
		Eccentricity    0.159941
		Inclination     29.0131
		AscendingNode   79.2872
		ArgOfPericenter 297.077
		MeanAnomaly     154.559
	}
}

DwarfMoon	"S2015 (136472) 1/MK 2"
{
	ParentBody     "Makemake"
	Class          "Asteroid"

	Radius          87.5

	Albedo          0.07

	Orbit
	{
		RefPlane       "Equator"
		Period          0.03395
		SemiMajorAxis   0.000141
	}
}

DwarfPlanet	"Eris/(136199) Eris"
{
	ParentBody     "Sol"

	Radius          1163
	Mass            0.0028

	RotationPeriod  25.9
	EqAscendNode    142  // used Dysnomia's orbit AscendingNode
	Obliquity       139  // used Dysnomia's orbit Inclination

	AbsMagn        -1.2
	SlopeParam      0.15
	Albedo          0.96
	Brightness      1.5 // 5.9

	Surface
	{
		colorSea       (1.000 1.000 1.000 0.500)
		colorShelf     (0.950 0.950 0.950 0.500)
		colorBeach     (0.750 0.750 0.750 1.000)
		colorDesert    (0.850 0.850 0.850 1.000)
		colorLowland   (0.870 0.870 0.870 1.000)
		colorUpland    (0.930 0.930 0.930 1.000)
		colorRock      (1.000 1.000 1.000 1.000)
		colorSnow      (1.000 1.000 1.000 1.000)
		colorLowPlants (0.870 0.870 0.870 1.000)
		colorUpPlants  (0.930 0.930 0.930 1.000)
		DayAmbient      0.2
		Hapke           0.8
	}

	Orbit
	{
		RefPlane       "Ecliptic"
		Epoch           2456401
		MeanMotion      0.00175931
		SemiMajorAxis   67.9581
		Eccentricity    0.437079
		Inclination     43.8851
		AscendingNode   36.031
		ArgOfPericenter 150.804
		MeanAnomaly     203.207
	}
}

DwarfMoon	"Dysnomia"
{
	ParentBody     "Eris"

	Radius          342

	Color         ( 0.480 0.400 0.380 )
	Brightness      2.0 // 4.0

	Surface
	{
		hillsMagn	0
		hillsFreq	0
	}

	Orbit
	{
		RefPlane       "Ecliptic"
		Epoch           2453979.0 // 2006.08.31 12:00
		Period          0.0431823
		SemiMajorAxis   2.5021e-4
		Inclination     142
		AscendingNode   139 // unknown
		MeanAnomaly     328.6
	}
}
