////////////////////////////////////////////////////////////
//                                                        //
//         Exoplanet catalog for SpaceEngine 0.974        //
//                                                        //
//             Last update : 2015-12-16                   //
//                                                        //
// Exoplanets with additional data (color, rings etc)     //
//                                                        //
// Original data from:                                    //
//                                                        //
// 1) NASA Exoplanet Archive                              //
//    http://exoplanetarchive.ipac.caltech.edu/index.html //
// 2) Interactive Exoplanet Catalog                       //
//    http://exoplanet.eu/catalog/                        //
//    Jean Schneider (CNRS-LUTH, Paris Observatory)       //
//                                                        //
////////////////////////////////////////////////////////////

// Star solver log level:
// 0 - do not log
// 1 - log errors and warnings only
// 2 - log everything
LogLevel    2


Planet	"1SWASP J1407 b"
{
	ParentBody     "1SWASP J1407"
	Mass            6364
	DiscMethod     "Transit"
	DiscDate       "2012"
	Rings
	{
		InnerRadius 90e3
		OuterRadius 90e6
		Texture     "J1407b-rings.*"
		FrontBright 1
		BackBright  0.15
		Brightness  2
		Density     2
	}
	Orbit
	{
		Period         10.19871226
		SemiMajorAxis  3.9
	}
}

Planet	"HD 189733 b"
{
	ParentBody     "HD 189733"
	Msini           362.1116
	Radius          81357.896
	DiscMethod     "Transit"
	DiscDate       "2005"
	AlbedoGeom      0.4

	NoClouds        true

	Atmosphere
	{
		Height   500
		Model   "Neptune"
	}

	Orbit
	{
		Epoch           2454037.612
		Period          0.006074252049
		SemiMajorAxis   0.03142
		Eccentricity    0.0041
		Inclination     85.51
		ArgOfPericen    90
	}
}

Planet	"Galileo/55 Cnc b"
{
	ParentBody     "55 Cnc A"
	Mass            264.29692
	DiscMethod     "RadVel"
	DiscDate       "1996"
	Orbit
	{
		Epoch          2453035
		Period         0.04011453333
		SemiMajorAxis  0.115227
		Eccentricity   0.0034
		Inclination    90
		ArgOfPericen   98
	}
}

Planet	"Brahe/55 Cnc c"
{
	ParentBody     "55 Cnc A"
	Mass            54.53948
	DiscMethod     "RadVel"
	DiscDate       "2004"
	Orbit
	{
		Epoch          2449989.339
		Period         0.1216110878
		SemiMajorAxis  0.241376
		Eccentricity   0.02
		Inclination    90
		ArgOfPericen   51
	}
}

Planet	"Lippershey/55 Cnc d"
{
	ParentBody     "55 Cnc A"
	Mass            1233.9796
	DiscMethod     "RadVel"
	DiscDate       "2002"
	Orbit
	{
		Epoch          2452500.6
		Period         13.21041253
		SemiMajorAxis  5.503
		Eccentricity   0.019
		Inclination    90
		ArgOfPericen   44
	}
}

Planet	"Janssen/55 Cnc e"
{
	ParentBody     "55 Cnc A"
	Msini           8.329685681
	Radius          12153.64
	DiscMethod     "RadVel"
	DiscDate       "2004"
	Orbit
	{
		Epoch          2449999.836
		Period         0.002016584394
		SemiMajorAxis  0.01544
		Eccentricity   0.06
		Inclination    83
		ArgOfPericen   170
	}
}

Planet	"Harriot/55 Cnc f"
{
	ParentBody     "55 Cnc A"
	Mass            44.8662
	DiscMethod     "RadVel"
	DiscDate       "2007"
	Orbit
	{
		Epoch          2450080.911
		Period         0.717332245
		SemiMajorAxis  0.788
		Eccentricity   0.305
		Inclination    90
		ArgOfPericen   166
	}
}

Planet	"Kepler-1 b/KOI-1.01/TrES-2 b"
{
	ParentBody     "Kepler-1"
	Mass            380.8854
	Radius          89150.524
	DiscMethod     "Transit"
	DiscDate       "2006"
	Albedo          0.0004
	Brightness      0.4
	Orbit
	{
		Period         0.006764314854
		SemiMajorAxis  0.0367
		Eccentricity   0
		Inclination    83.872
	}
}

Planet	"Osiris/HD 209458 b"
{
	ParentBody     "HD 209458"
	Msini           219.558
	Radius          98658.96
	DiscMethod     "Transit"
	DiscDate       "1999"
	AlbedoGeom      0.038
	Brightness      0.7
	Orbit
	{
		Epoch           2452968.399
		Period          0.00965036378
		SemiMajorAxis   0.04747
		Eccentricity    0.0082
		Inclination     86.59
		ArgOfPericen    43.8
	}
}

Planet	"Kepler-444 A b/KOI-3158.01"
{
	ParentBody     "Kepler-444 A"
	Radius          2430.728
	DiscMethod     "Transit"
	DiscDate       "2015"
	Orbit
	{
		Period         0.009856502425
		SemiMajorAxis  0.04178
		Eccentricity   0.08
		Inclination    88
		AscendingNode  73	// co-planar with (BC) pair
	}
}

Planet	"Kepler-444 A c/KOI-3158.02"
{
	ParentBody     "Kepler-444 A"
	Radius          3124.2004
	DiscMethod     "Transit"
	DiscDate       "2015"
	Orbit
	{
		Period         0.0124462185
		SemiMajorAxis  0.04881
		Eccentricity   0.12
		Inclination    88.2
		AscendingNode  73	// co-planar with (BC) pair
	}
}

Planet	"Kepler-444 A d/KOI-3158.03"
{
	ParentBody     "Kepler-444 A"
	Radius          3381.5716
	DiscMethod     "Transit"
	DiscDate       "2015"
	Orbit
	{
		Period         0.01694599412
		SemiMajorAxis  0.06
		Eccentricity   0.18
		Inclination    88.16
		AscendingNode  73	// co-planar with (BC) pair
	}
}

Planet	"Kepler-444 A e/KOI-3158.04"
{
	ParentBody     "Kepler-444 A"
	Radius          3403.0192
	DiscMethod     "Transit"
	DiscDate       "2015"
	Orbit
	{
		Period         0.02120098175
		SemiMajorAxis  0.0696
		Eccentricity   0.02
		Inclination    89.13
		AscendingNode  73	// co-planar with (BC) pair
	}
}

Planet	"Kepler-444 A f/KOI-3158.05"
{
	ParentBody     "Kepler-444 A"
	Radius          4332.4152
	DiscMethod     "Transit"
	DiscDate       "2015"
	Orbit
	{
		Period         0.02666856752
		SemiMajorAxis  0.0811
		Eccentricity   0.58
		Inclination    87.96
		AscendingNode  73	// co-planar with (BC) pair
	}
}

