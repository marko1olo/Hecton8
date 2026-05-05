/////////////////////////////////////////////////////////////////////
//                                                                 //
//	Catalog of binary/multiple star systems (ssytem barycenters)   //
//                                                                 //
//                   52 Constellations done                        //
//                                                                 //
// Andromeda, Aquarius, Aquila, Ara, Aries, Auriga, Bootes,		   //
// Camelopardalis, Capricornus, Carina, Crux, Cygnus, Delphinus,   //
// Draco, Equuleus, Eridanus, Gemini, Hercules, Leo, Libra,        //
// Pegasus, Serpens, Scorpius, Scutum, Sagitta, Sagittarius,       //
// Monoceros, Ophiuchus, Orion, Perseus, Piscis, Puppis, Pyxis,    //
// Triangulum, Ursa Major, Ursa Minor, Vela, Virgo, Vulpecula,     //
// Lacerta, Leo Minor,Lynx, Canis Venaciti, Lyra                   //
//                                                                 //
/////////////////////////////////////////////////////////////////////

///////////////////////////////SOURCES////////////////////////////////
//			Special mention to Jim Kaler Stars website:		        //
//		http://stars.astro.illinois.edu/sow/sowlist.html		    //
//by Jim Kaler, Prof. Emeritus of Astronomy, University of Illinois //
//////////////////////////////////////////////////////////////////////
//				The 6th Catalogue of Visual Binaries				//
//////////////////////////////////////////////////////////////////////
//		The wikipedia, specially english and spanish versions		//
//						http://www.wikipedia.org				    //
//////////////////////////////////////////////////////////////////////
//					SIMBAD Astronomical Database					//
//					 http://simbad.u-strasbg.fr						//
//////////////////////////////////////////////////////////////////////
//Various astronomical papers properly commented in each star system//
//////////////////////////////////////////////////////////////////////

// Star solver log level:
// 0 - do not log
// 1 - log errors and warnings only
// 2 - log everything
LogLevel    1

////////////////////////////ANDROMEDA//////////////////////////////////
///////////////////////////////////////////////////////////////////////


//PSI And;english wiki

Star "PSI And A/HIP 117221/HD 223047" //3rd companion, unknown class
{
	ParentBody "PSI And"
	Class      "G5 Ib"
	AppMagn    4.95
	MassSol    5.4
	Orbit
	{
		Period          276.9368 		//generic
		SemiMajorAxis   29.2919 		//just observed separation
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "PSI And B"
{
	ParentBody "PSI And"
	Class      "B9 V"  					//unknown luminosity class
	Orbit
	{
		Period          276.9368
		SemiMajorAxis   56.4914
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


//PI And;6thCV,english wiki and spanish wiki


Barycenter "PI And (AC)"
{
	ParentBody "PI And"
	Orbit
	{
		Period          175000
		SemiMajorAxis   1047.3768
		Inclination     103
		AscendingNode   94.7
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Barycenter "PI And A"
{
	ParentBody "PI And (AC)"
	Orbit
	{
		Period          80
		SemiMajorAxis   2.7907
		Inclination     103
		AscendingNode   94.7
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "PI And Aa/HIP 2912/HD 3369"
{
	ParentBody "PI And A"
	Class      "B5 V"
	AppMagn    4.34
	MassSol    5
	Orbit
	{
		Period          0.3932
		SemiMajorAxis   0.6731
		Eccentricity    0.542
		Inclination     103
		AscendingNode   94.7
		ArgOfPericenter 170.7
		Epoch           2447717.7
		MeanAnomaly     0
	}
}

Star "PI And Ab"
{
	ParentBody "PI And A"
	Class      "B V"
	MassSol    5
	Orbit
	{
		Period          0.3932
		SemiMajorAxis   0.6731
		Eccentricity    0.542
		Inclination     103
		AscendingNode   94.7
		ArgOfPericenter 350.7
		Epoch           2447717.7
		MeanAnomaly     0
	}
}

Star "PI And C"
{
	ParentBody "PI And (AC)"
	Class      "K V"
	Orbit
	{
		Period          80
		SemiMajorAxis   37.2093
		Inclination     103
		AscendingNode   94.7
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "PI And B"
{
	ParentBody "PI And"
	Class      "A6 V"
	AppMagn    8.61
	Orbit
	{
		Period          175000
		SemiMajorAxis   6152.6232
		Inclination     103
		AscendingNode   94.7
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}



//NU And; english wiki  

Star "NU And A/HIP 3881/HD 4727"
{
	ParentBody "NU And"
	Class      "B9 V"
	MassSol    5.9
	Radius     2380000
	Orbit
	{
		Period          0.0117
		SemiMajorAxis   0.0165
		Eccentricity    0.03
		AscendingNode   25
		ArgOfPericenter 0
		Epoch           2418155.67
		MeanAnomaly     0
	}
}

Star "NU And B"
{
	ParentBody "NU And"
	Class      "F8 V"
	MassSol    1.18
	Orbit
	{
		Period          0.0117
		SemiMajorAxis   0.0827
		Eccentricity    0.03
		AscendingNode   25
		ArgOfPericenter 180
		Epoch           2418155.67
		MeanAnomaly     0
	}
}

//OMI And;6thCVB,english wiki

Barycenter "OMI And A"
{
	ParentBody "OMI And"
	Orbit
	{
		Period          117.4
		SemiMajorAxis   20.3654
		Eccentricity    0.371
		Inclination     109.6
		AscendingNode   5.6
		ArgOfPericenter 144.2
		Epoch           2455050.858506
		MeanAnomaly     0
	}
}

Star "OMI And Aa/HIP 113726/HD 217675"
{
	ParentBody "OMI And A"
	Class      "G8 III"
	AppMagn    3.62
	MassSol    7
	Orbit
	{
		Period          5.64
		SemiMajorAxis   1.5792
		Eccentricity    0.22
		Inclination     152
		AscendingNode   318
		ArgOfPericenter 55
		Epoch           2452859.405314
		MeanAnomaly     0
	}
}

Star "OMI And Ab"
{
	ParentBody "OMI And A"
	AppMagn    7 			//unknown,SP companion
	Orbit
	{
		Period          5.64
		SemiMajorAxis   11.0542
		Eccentricity    0.22
		Inclination     152
		AscendingNode   318
		ArgOfPericenter 235
		Epoch           2452859.405314
		MeanAnomaly     0
	}
}


Barycenter "OMI And B"
{
	ParentBody "OMI And"
	Orbit
	{
		Period          117.4
		SemiMajorAxis   40.7307
		Eccentricity    0.371
		Inclination     109.6
		AscendingNode   5.6
		ArgOfPericenter 324.2
		Epoch           2455050.858506
		MeanAnomaly     0
	}
}

Star "OMI And Ba"
{
	ParentBody "OMI And B"
	Class      "A V"
	Orbit
	{
		Period          33.01
		SemiMajorAxis   8.167
		Inclination     109.6
		AscendingNode   5.6
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "OMI And Bb"
{
	ParentBody "OMI And B"
	AppMagn    7 				//unknown,SP companion
	Orbit
	{
		Period          33.01
		SemiMajorAxis   8.167
		Inclination     109.6
		AscendingNode   5.6
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//Almach,GAM And;6thCVB,english wiki


Barycenter "GAM2 And/GAM And BC/ADS 1630 BC/WDS 02039+4220 BC"
{
	ParentBody "Almach"
	Orbit
	{
		SemiMajorAxis   1029.3997
		Inclination     109.8
		AscendingNode   109.6
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Barycenter "GAM2 And B/HIP 9640/HD 12534"
{
	ParentBody "GAM2 And"
	Orbit
	{
		Period          63.67
		SemiMajorAxis   7.3725
		Eccentricity    0.927
		Inclination     109.8
		AscendingNode   109.6
		ArgOfPericenter 183.4
		Epoch           2457205.787479
		MeanAnomaly     0
	}
}

Star "GAM1 And/57 And A/STF 205A/ADS 1630 A/WDS 02039+4220 A"
{
	ParentBody "Almach"
	Class      "K3 II"
	AppMagn    2.26
	Radius     56000000
	Orbit
	{
		SemiMajorAxis   1029.3997
		Inclination     109.8
		AscendingNode   109.6
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "GAM2 And Ba"
{
	ParentBody "GAM2 And B"
	Class      "B9 V"
	MassSol    3.22279
	Orbit
	{
		Period          0.007315068
		SemiMajorAxis   0.070217272
		Inclination     109.8
		AscendingNode   109.6
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "GAM2 And Bb"
{
	ParentBody "GAM2 And B"
	Class      "B9 V"
	MassSol    3.22279
	Orbit
	{
		Period          0.007315068
		SemiMajorAxis   0.070217272
		Inclination     109.8
		AscendingNode   109.6
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


Star "GAM2 And C"
{
	ParentBody "GAM2 And"
	Class      "A V"
	AppMagn    6.3
	MassSol    1.9
	Orbit
	{
		Period          63.67
		SemiMajorAxis   25.0107
		Eccentricity    0.927
		Inclination     109.8
		AscendingNode   109.6
		ArgOfPericenter 3.4
		Epoch           2457205.787479
		MeanAnomaly     0
	}
}

//ETA And;6thCVB, english wiki

Star "ETA And A/HIP 4463/HD 5516"
{
	ParentBody "ETA And"
	Class      "G8 III"
	AbsMagn    0.52 
	MassSol    2.6 //Mass wiki
	Orbit
	{
		Period          0.317
		SemiMajorAxis   0.3579
		Eccentricity    0.006
		Inclination     30.5
		AscendingNode   69.4
		ArgOfPericenter 215
		Epoch           2448013
		MeanAnomaly     0
	}
}

Star "ETA And B"
{
	ParentBody "ETA And"
	Class      "G8 III"
	AbsMagn    1.07
	MassSol    2.3  //Mass wiki
	Orbit
	{
		Period          0.317
		SemiMajorAxis   0.4046
		Eccentricity    0.006
		Inclination     30.5
		AscendingNode   69.4
		ArgOfPericenter 35
		Epoch           2448013
		MeanAnomaly     0
	}
}

//DEL And;6thCVB,english wiki


Barycenter "DEL And A"
{
	ParentBody "DEL And"
	Orbit
	{
		Period          15733.41
		SemiMajorAxis   122.0339
		Inclination     137  //unknown just aligned with the system
		AscendingNode   290
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "DEL And Aa/HIP 3092/HD 3627"
{
	ParentBody "DEL And A"
	Class      "K3 III"
	Radius     9396000
	AppMagn    3.28
	MassSol    1.8
	Orbit
	{
		Period          52.8
		SemiMajorAxis   5.9013
		Eccentricity    0.5
		Inclination     137
		AscendingNode   290
		ArgOfPericenter 231
		Epoch           2436386.982149
		MeanAnomaly     0
	}
}

Star "DEL And Ab"
{
	ParentBody "DEL And A"
	AppMagn    7 					//unknown, SP companion
	Orbit
	{
		Period          52.8
		SemiMajorAxis   14.1631
		Eccentricity    0.5
		Inclination     137
		AscendingNode   290
		ArgOfPericenter 51
		Epoch           2436386.982149
		MeanAnomaly     0
	}
}

Star "DEL And B"
{
	ParentBody "DEL And"
	Class      "M3 V"
	AppMagn    12.4
	Orbit
	{
		Period          15733.41
		SemiMajorAxis   777.9661
		Inclination     137  		//unknown just aligned with the system
		AscendingNode   290
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//Alpheratz;6thCVB,english wiki

Star "Alpheratz A/Sirrah/ALF And A/HIP 677/HD 358"
{
	ParentBody "Alpheratz"
	Class      "B8 IV"
	AppMagn    2.22
	MassSol    3.6
	Radius     1879200 //wiki
	Orbit
	{
		Period          0.2649
		SemiMajorAxis   0.236
		Eccentricity    0.535
		Inclination     105.6
		AscendingNode   284.4
		ArgOfPericenter 257.4
		Epoch           2447374.563
		MeanAnomaly     0
	}
}

Star "Alpheratz B/ALF And B"
{
	ParentBody "Alpheratz"
	Class      "A3 V"
	AppMagn    4.21
	MassSol    1.78
	Radius     1148400 				
	Orbit
	{
		Period          0.2649
		SemiMajorAxis   0.4772
		Eccentricity    0.535
		Inclination     105.6
		AscendingNode   284.4
		ArgOfPericenter 77.4
		Epoch           2447374.563
		MeanAnomaly     0
	}
}

//HR 483;6thCVB,english wiki

Star "HR 483 A/HIP 7918/HD 10307"
{
	ParentBody "HR 483"
	Class      "G1 V"
	AppMagn    4.95
	MassSol    0.97 		
	Orbit
	{
		Period          19.5
		SemiMajorAxis   1.685
		Eccentricity    0.43
		Inclination     105
		AscendingNode   33
		ArgOfPericenter 22
		Epoch           2450485.331022
		MeanAnomaly     0
	}
}

Star "HR 483 B"
{
	ParentBody "HR 483"
	Class      "M V"
	AppMagn    11
	MassSol    0.29 //Mass wiki
	Orbit
	{
		Period          19.5
		SemiMajorAxis   5.636
		Eccentricity    0.43
		Inclination     105
		AscendingNode   33
		ArgOfPericenter 158
		Epoch           2450485.331022
		MeanAnomaly     0
	}
}

//2 And;6thCVB,english wiki


Star "2 And A/HIP 113788/HD 217782"
{
	ParentBody "2 And"
	Class      "A3 V"
	AppMagn    5.19
	MassSol    2.415
	Orbit
	{
		Period          73.997
		SemiMajorAxis   13.1141
		Eccentricity    0.8
		Inclination     21.7
		AscendingNode   159.5
		ArgOfPericenter 356.4
		Epoch           2404165.315372
		MeanAnomaly     0
	}
}

Star "2 And B"
{
	ParentBody "2 And"
	Class      "A V" 			//unknown,related with appMagn
	AppMagn    7.7
	MassSol    2 
	Orbit
	{
		Period          73.997
		SemiMajorAxis   15.8378
		Eccentricity    0.8
		Inclination     21.7
		AscendingNode   159.5
		ArgOfPericenter 176.4
		Epoch           2404165.315372
		MeanAnomaly     0
	}
}

//BX And
//semi-detached contact binary, with theorical 3rd component
//"The light curve and period variation of BX Andromedae"
//Authors: O. Demircan, A. Akalin and E. derman
//Data table from: Bell et al. 1990
//6thCVB parameters for the 3rd component

Barycenter "BX And A"
{
	ParentBody "BX And"
	Orbit
	{
		Period          62
		SemiMajorAxis   2.5681
		Eccentricity    0.3
		Inclination     74.5
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "BX And Aa/HIP 10027/HD 13078"
{
	ParentBody "BX And A"
	Class      "F2 V"
	Radius     1238880
	AppMagn    8.98
	MassSol    1.52
	Orbit
	{
		Period          0.00167154
		SemiMajorAxis   0.0061
		Inclination     74.5
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "BX And Ab"
{
	ParentBody "BX And A"
	Class      "F V"
	Radius     904800
	MassSol    0.75
	Orbit
	{
		Period          0.00167154
		SemiMajorAxis   0.0124
		Inclination     74.5
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "BX And B" 				//unconfirmed
{
	ParentBody "BX And"
	MassSol    0.3 
	Orbit
	{
		Period          62
		SemiMajorAxis   19.4319
		Eccentricity    0.3
		Inclination     74.5
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//6 Per;6thCVB, spanish wiki
//B component unknown


Star "6 Per A/HIP 10366/HD 13530"
{
	ParentBody "6 Per"
	Class      "G8 III"
	Radius     5011200
	AppMagn    5.31
	Orbit
	{
		Period          4.5205
		SemiMajorAxis   0.5446
		Eccentricity    0.75
		Inclination     115.6
		AscendingNode   168.33
		ArgOfPericenter 270
		Epoch           2447205.4414
		MeanAnomaly     0
	}
}

Star "6 Per B"
{
	ParentBody "6 Per"
	AppMagn    10 //unknown,SP companion
	Orbit
	{
		Period          4.5205
		SemiMajorAxis   0.5446
		Eccentricity    0.75
		Inclination     115.6
		AscendingNode   168.33
		ArgOfPericenter 90
		Epoch           2447205.4414
		MeanAnomaly     0
	}
}

//Mirach;spanish wiki, prof. jim kaler website


Star "Mirach A/HIP 5447/HD 6860"
{
	ParentBody "Mirach"
	Class      "M0 III"
	Radius     59856000
	AppMagn    2.1
	MassSol    3.5
	Orbit
	{
		Period          34988.9177
		SemiMajorAxis   219.9005
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Mirach B"
{
	ParentBody "Mirach"
	Class      "M V"
	AppMagn    14
	Orbit
	{
		Period          34988.9177
		SemiMajorAxis   1480.0995
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//FF And;spanish wiki


Barycenter "FF And (AB)"
{
	ParentBody "FF And"
	Orbit
	{
		Period          6.4602739726
		SemiMajorAxis   0.4437
		Inclination     60
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "FF And A"
{
	ParentBody "FF And (AB)"
	Class      "M1 V"
	Radius     452400
	AppMagn    10.38
	MassSol    0.5
	Orbit
	{
		Period          0.00594603
		SemiMajorAxis   0.015
		Inclination     60
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "FF And B"
{
	ParentBody "FF And (AB)"
	Class      "M1 V"
	Radius     452400
	MassSol    0.5
	Orbit
	{
		Period          0.00594603
		SemiMajorAxis   0.015
		Inclination     60
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "FF And C"
{
	ParentBody "FF And"
	Class      "M V"
	MassSol    0.09
	Orbit
	{
		Period          6.4603
		SemiMajorAxis   4.9295
		Inclination     60 //unknown just aligned
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//GY And;spanish wiki

Star "GY And A/HD 9996"
{
	ParentBody "GY And"
	Class      "B9 V"
	AppMagn    6.41
	MassSol    2.5
	Orbit
	{
		Period          0.7534
		SemiMajorAxis   0.7075
		Eccentricity    0.47
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "GY And B"
{
	ParentBody "GY And"
	AppMagn    13 					//unknown,SP companion
	Orbit
	{
		Period          0.7534
		SemiMajorAxis   0.7075
		Eccentricity    0.47
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//KZ And;spanish wiki


Barycenter "KZ And B"
{
	ParentBody "KZ And"
	Orbit
	{
		Period          6272
		SemiMajorAxis   180.6407
		Inclination     58
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "KZ And A/HIP 114379/HD 218378"
{
	ParentBody "KZ And"
	Class      "G5 V"
	AbsMagn    5.19
	MassSol    0.95
	Orbit
	{
		Period          6272
		SemiMajorAxis   273.8133
		Inclination     58
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "KZ And Ba"
{
	ParentBody "KZ And B"
	Class      "K2 V"
	Radius     515040
	AbsMagn    6.03
	MassSol    0.74
	Orbit
	{
		Period          3.033
		SemiMajorAxis   0.0225
		Inclination     58
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "KZ And Bb"
{
	ParentBody "KZ And B"
	Class      "K2 V"
	Radius     480240
	MassSol    0.7
	Orbit
	{
		Period          3.033
		SemiMajorAxis   0.0238
		Inclination     58
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


//OME And;spanish wiki


Star "OME And A/HIP 6813/HD 8799"
{
	ParentBody "OME And"
	Class      "F5 IV"
	Radius     1482480
	AppMagn    4.83
	MassSol    1.46
	Orbit
	{
		Period          54.0205
		SemiMajorAxis   6.9471
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}


Star "OME And B"
{
	ParentBody "OME And"
	Class      "K V" //unknown,related with AppMagn
	AppMagn    8.48
	Orbit
	{
		Period          54.0205
		SemiMajorAxis   11.9327
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//PHI And;6thCVB, spanish wiki

Star "PHI And A/HIP 5434/HD 6811"
{
	ParentBody "PHI And"
	Class      "B6 IV"
	Radius     5289600
	AppMagn    4.59
	Orbit
	{
		Period          554
		SemiMajorAxis   65.0337
		Eccentricity    0.385
		Inclination     142.2
		AscendingNode   337.2
		ArgOfPericenter 112.6
		Epoch           2417741.367901
		MeanAnomaly     0
	}
}

Star "PHI And B"
{
	ParentBody "PHI And"
	Class      "B9 V"
	Radius     3549600
	AppMagn    5.61
	Orbit
	{
		Period          554
		SemiMajorAxis   65.0337
		Eccentricity    0.385
		Inclination     142.2
		AscendingNode   337.2
		ArgOfPericenter 292.6
		Epoch           2417741.367901
		MeanAnomaly     0
	}
}

//QX And;spanish wiki
//contact binary

Star "QX And A"
{
	ParentBody "QX And"
	Class      "F5 V"
	Radius     1016160
	AppMagn    11.25
	MassSol    1.47
	Orbit
	{
		Period          0.0011
		SemiMajorAxis   0.0032
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "QX And B"
{
	ParentBody "QX And"
	Class      "F V" 
	Radius     612480
	MassSol    0.45
	Orbit
	{
		Period          0.0011
		SemiMajorAxis   0.0103
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//TAU And;spanish wiki

Star "TAU And A/HIP 7818/HD 10205"
{
	ParentBody "TAU And"
	Class      "B8 III"
	Radius     4176000
	AppMagn    4.96
	MassSol    4.5
	Orbit
	{
		Period          1415.1242
		SemiMajorAxis   2064.9191
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "TAU And B"
{
	ParentBody "TAU And"
	Class      "G V" //unknown, related with     AppMagn
	AppMagn    11.5
	Orbit
	{
		Period          1415.1242
		SemiMajorAxis   9292.1361
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//V405 And;spanish wiki

Star "V405 And A"
{
	ParentBody "V405 And"
	Class      "M0 V"
	Radius     542880
	AppMagn    11
	MassSol    0.49
	Orbit
	{
		Period          0.0013
		SemiMajorAxis   0.0031
		Inclination     66.5
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "V405 And B"
{
	ParentBody "V405 And"
	Class      "M5 V"
	Radius     167040
	MassSol    0.21
	Orbit
	{
		Period          0.0013
		SemiMajorAxis   0.0073
		Inclination     66.5
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//ZET And;spanish wiki

Star "ZET And A/HIP 3693/HD 4502"
{
	ParentBody "ZET And"
	Class      "K1 III"
	Radius     11066400
	AppMagn    4.1
	MassSol    2.6
	Orbit
	{
		Period          0.0487
		SemiMajorAxis   0.0554
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "ZET And B"
{
	ParentBody "ZET And"
	Class      "G V"
	Orbit
	{
		Period          0.0487
		SemiMajorAxis   0.144
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

///////////////////////END OF ANDROMEDA /////////////////////////////////////////

/////////////////////////////////////////////////////////////////////
///////////////////////URSA MAJOR////////////////////////////////////
/////////////////////////////////////////////////////////////////////

Barycenter	"Mizar A/ZET UMa A/79 UMa A"
{
	ParentBody  "Mizar"
	AppMagn      2.23
	MassSol      5.0

	Orbit
	{
		Period              2000	// approximate
		SemiMajorAxis      148.3	// 380 * mass ratio 5:3.2
		Eccentricity         0.5	// unknown
		Inclination         88.219	// used Mizar Aa/Ab data
		AscendingNode       98.364	// used Mizar Aa/Ab data
		ArgOfPericenter      0.0	// unknown
		MeanAnomaly          0.0	// unknown
	}
}

Barycenter	"Mizar B/ZET UMa B/79 UMa B"
{
	ParentBody  "Mizar"
	AppMagn      3.95
	MassSol      3.2

	Orbit
	{
		Period              2000	// approximate
		SemiMajorAxis      231.7	// 380 * mass ratio 5:3.2
		Eccentricity         0.5	// unknown
		Inclination         88.219	// used Mizar Aa/Ab data
		AscendingNode       98.364	// used Mizar Aa/Ab data
		ArgOfPericenter    180.0	// unknown
		MeanAnomaly          0.0	// unknown
	}
}

Star	"Mizar Aa"
{
	ParentBody  "Mizar A"
	Class       "A1V"
	AppMagn      2.23
	MassSol      2.56

	Orbit
	{
		Period               0.056
		SemiMajorAxis        0.122 // 0.25 * mass ratio 1.051
		Eccentricity         0.529
		Inclination         88.219
		AscendingNode       98.364
		ArgOfPericenter     91.381
		MeanAnomaly        104.896
	}
}

Star	"Mizar Ab"
{
	ParentBody  "Mizar A"
	Class       "A2V"
	AppMagn      2.23
	MassSol      2.44

	Orbit
	{
		Period               0.056
		SemiMajorAxis        0.128 // 0.25 * mass ratio 1.051
		Eccentricity         0.529
		Inclination         88.219
		AscendingNode       98.364
		ArgOfPericenter    271.381
		MeanAnomaly        104.896
	}
}

Star	"Mizar Ba"
{
	ParentBody  "Mizar B"
	Class       "A7V"
	AppMagn      3.95
	MassSol      1.6

	Orbit
	{
		Period               0.5
		Eccentricity         0.5	// unknown
		Inclination         88.219	// used Mizar Aa/Ab data
		AscendingNode       98.364	// used Mizar Aa/Ab data
		ArgOfPericenter    180.0	// unknown
		MeanAnomaly          0.0	// unknown
	}
}

Star	"Mizar Bb"
{
	ParentBody  "Mizar B"
	Class       "A7V"
	AppMagn      3.95
	MassSol      1.6

	Orbit
	{
		Period               0.5
		Eccentricity         0.5	// unknown
		Inclination         88.219	// used Mizar Aa/Ab data
		AscendingNode       98.364	// used Mizar Aa/Ab data
		ArgOfPericenter      0.0	// unknown
		MeanAnomaly          0.0	// unknown
	}
}

Star	"Alcor A"
{
	ParentBody  "Alcor"
	Class       "A5V"
	AppMagn      3.99
	MassSol      2.0

	Orbit
	{
		Period               90
		Eccentricity         0.5
		Inclination          0.0
		AscendingNode        0.0
		ArgOfPericenter      0.0
		MeanAnomaly          0.0
	}
}

Star	"Alcor B"
{
	ParentBody  "Alcor"
	Class       "M3V"
	MassSol      0.25

	Orbit
	{
		Period               90
		Eccentricity         0.5
		Inclination          0.0
		AscendingNode        0.0
		ArgOfPericenter    180.0
		MeanAnomaly          0.0
	}
}

//UPS UMa;eng and sp wiki

Star "UPS UMa A/HIP 48319/HD 84999"
{
	ParentBody "UPS UMa"
	Class      "F2 V"
	Radius     2436000
	AppMagn    3.8
	MassSol    2
	Orbit
	{
		Period          5239.71292976
		SemiMajorAxis   81.8405
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "UPS UMa B"
{
	ParentBody "UPS UMa"
	Class      "M0 V"
	AppMagn    11.5
	MassSol    0.5
	Orbit
	{
		Period          5239.71292976
		SemiMajorAxis   327.362
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//Al Haud;spanish and english wiki

Barycenter "Al Haud A"
{
	ParentBody "Al Haud"
	Orbit
	{
		Period          231.19
		SemiMajorAxis   2.7923
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Al Haud Aa/HIP 46853/HD 82328"
{
	ParentBody "Al Haud A"
	Class      "F6 IV"
	Radius     1646040
	AppMagn    3.17
	MassSol    1.41
	Orbit
	{
		Period          1.01643836
		SemiMajorAxis   0.7299
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Al Haud Ab"
{
	ParentBody "Al Haud A"
	AppMagn    7 //unknown,SP companion
	Orbit
	{
		Period          1.01643836
		SemiMajorAxis   0.7299
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "Al Haud B"
{
	ParentBody "Al Haud"
	Class      "M6 V"
	AppMagn    13.8
	MassSol    0.15
	Orbit
	{
		Period          231.19
		SemiMajorAxis   52.4948
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


//Tania Australis A; 6thCVB, eng wiki
//separation of 1.5 au instead 6thCVB
//total Mass for the system of 9 MS
//A member is more evolved so it should be bigger (5 MS)
//companion had rest of the Mass, unknown if it's in the main sequence

Star "Tania Australis A/HIP 50801/HD 89758"
{
	ParentBody "Tania Australis"
	Class      "M0 III"
	MassSol    5
	Radius     43152000
	AppMagn    3.07
	Orbit
	{
		Period          0.63038082
		SemiMajorAxis   0.6667
		Eccentricity    0.06
		Inclination     13.6
		AscendingNode   263.6
		ArgOfPericenter 236.4
		Epoch           2425577.03
		MeanAnomaly     0
	}
}

Star "Tania Australis B"
{
	ParentBody "Tania Australis"
	MassSol    4
	Class      "A V" //unknown, Class according to supposed 4 MS 
	Orbit
	{
		Period          0.63038082
		SemiMajorAxis   0.8333
		Eccentricity    0.06
		Inclination     13.6
		AscendingNode   263.6
		ArgOfPericenter 56.4
		Epoch           2425577.03
		MeanAnomaly     0
	}
}

//Talitha Aus;6thCVB,eng wiki

Star "Talitha Australis A/HIP 44471/HD 77327"
{
	ParentBody "Talitha Australis"
	Class      "A0 IV"
	AppMagn    4.16
	MassSol    3.4
	Orbit
	{
		Period          35.6362
		SemiMajorAxis   9.99
		Eccentricity    0.5584
		Inclination     109.41
		AscendingNode   105.641
		ArgOfPericenter 355.63
		Epoch           2450404
		MeanAnomaly     0
	}
}

Star "Talitha Australis B"
{
	ParentBody "Talitha Australis"
	Class      "A0 V"
	AppMagn    4.54
	MassSol    3.4
	Orbit
	{
		Period          35.6362
		SemiMajorAxis   9.99
		Eccentricity    0.5584
		Inclination     109.41
		AscendingNode   105.641
		ArgOfPericenter 175.63
		Epoch           2450404
		MeanAnomaly     0
	}
}


//PHI UMa;6thCVB, spanish wiki

Star "PHI UMa A/HIP 48402/HD 85235"
{
	ParentBody "PHI UMa"
	Class      "A3 IV"
	Radius     3271200
	AppMagn    5.28
	MassSol    3
	Orbit
	{
		Period          105.4
		SemiMajorAxis   28.1797
		Eccentricity    0.45
		Inclination     24.5
		AscendingNode   130.3
		ArgOfPericenter 35
		Epoch           2446942.481693
		MeanAnomaly     0
	}
}

Star "PHI UMa B"
{
	ParentBody "PHI UMa"
	Class      "A3 IV"
	Radius     3132000
	AppMagn    5.39
	MassSol    3.2
	Orbit
	{
		Period          105.4
		SemiMajorAxis   26.4185
		Eccentricity    0.45
		Inclination     24.5
		AscendingNode   130.3
		ArgOfPericenter 215
		Epoch           2446942.481693
		MeanAnomaly     0
	}
}


//Muscida; with exoplanet

//Talitha; 6thCVB, english and sp wiki
//A+BC period of 2084 years in 6thCVB
//but 818 in wiki???

Barycenter "Talitha Borealis A"
{
	ParentBody "Talitha Borealis"
	Orbit
	{
		Period          818
		SemiMajorAxis   30.75006199
		Eccentricity    0.9
		Inclination     54
		AscendingNode   134
		ArgOfPericenter 23
		Epoch           2462136.557163
		MeanAnomaly     0
	}
}

Barycenter "Talitha Borealis (BC)"
{
	ParentBody "Talitha Borealis"
	Orbit
	{
		Period          818
		SemiMajorAxis   101.249938
		Eccentricity    0.9
		Inclination     54
		AscendingNode   134
		ArgOfPericenter 203
		Epoch           2462136.557163
		MeanAnomaly     0
	}
}

Star "Talitha Borealis Aa/IOT UMa Aa/HIP 44127/HD 76644"
{
	ParentBody "Talitha Borealis A"
	Class      "A7 IV"
	AppMagn    3.14
	Orbit
	{
		Period          11.0356
		SemiMajorAxis   3.315
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Talitha Borealis Ab"
{
	ParentBody "Talitha Borealis A"
	AppMagn    6 //unknown spect companion
	Orbit
	{
		Period          11.0356
		SemiMajorAxis   3.315
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "Talitha Borealis B"
{
	ParentBody "Talitha Borealis (BC)"
	Class      "M1 V"
	AppMagn    10.1
	MassSol    0.52
	Orbit
	{
		Period          39.4
		SemiMajorAxis   5.0782
		Eccentricity    0.35
		Inclination     111.6
		AscendingNode   24.5
		ArgOfPericenter 354.2
		Epoch           2451215.815419
		MeanAnomaly     0
	}
}

Star "Talitha Borealis C"
{
	ParentBody "Talitha Borealis (BC)"
	Class      "M1 V"
	AppMagn    10.3
	MassSol    0.52
	Orbit
	{
		Period          39.4
		SemiMajorAxis   5.0782
		Eccentricity    0.35
		Inclination     111.6
		AscendingNode   24.5
		ArgOfPericenter 180
		Epoch           2451215.815419
		MeanAnomaly     0
	}
}

//Alula Borealis; spanish wiki

Star "Alula Borealis A/NU UMa A/HIP 55219/HD 98262"
{
	ParentBody "Alula Borealis"
	Class      "K3 III"
	Radius     45936000
	AppMagn    3.49
	MassSol    4
	Orbit
	{
		Period          13222.98407562
		SemiMajorAxis   191.1288
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Alula Borealis B"
{
	ParentBody "Alula Borealis"
	Class      "G1 V"
	AppMagn    10.1
	MassSol    1
	Orbit
	{
		Period          13222.98407562
		SemiMajorAxis   764.5153
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//Dubhe;6thCVB,english wiki

Star "Dubhe A/ALF UMa A/HIP 54061/HD 95689" //MULTIPLE, 2 MORE F STARS
{
	ParentBody "Dubhe"
	Class      "K1 II"
	AppMagn    2.02
	MassSol    4.652
	Orbit
	{
		Period          44.448
		SemiMajorAxis   3.5705
		Eccentricity    0.4392
		Inclination     159.9
		AscendingNode   9.3
		ArgOfPericenter 232.8
		Epoch           2452337.108969
		MeanAnomaly     0
	}
}

Star "Dubhe B/ALF UMa B"
{
	ParentBody "Dubhe"
	Class      "K0 V"
	AppMagn    4.95
	MassSol    0.89
	Orbit
	{
		Period          44.448
		SemiMajorAxis   18.6627
		Eccentricity    0.4392
		Inclination     159.9
		AscendingNode   9.3
		ArgOfPericenter 52.8
		Epoch           2452337.108969
		MeanAnomaly     0
	}
}

//Alula Australis;with brown dwarf

//16 UMa; spanish wiki, jim kaller stars website

Star "16 UMa A/HIP 45333/HD 79028"
{
	ParentBody "16 UMa"
	Class      "G0 V"
	Radius     1113600
	AppMagn    5.2
	MassSol    1.11
	Orbit
	{
		Period          0.04447389
		SemiMajorAxis   0.0372
		Eccentricity    0.09
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "16 UMa B"
{
	ParentBody "16 UMa"
	Class      "M V" //unknown, can be also a white dwarf
	MassSol    0.6
	Orbit
	{
		Period          0.04447389
		SemiMajorAxis   0.0688
		Eccentricity    0.09
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//23 UMa; spanish wiki, jim kaller stars website

Star "23 UMa A/HIP 46733/HD 81937"
{
	ParentBody "23 UMa"
	Class      "F0 IV"
	Radius     1740000
	AppMagn    3.75
	MassSol    1.5
	Orbit
	{
		Period          7900
		SemiMajorAxis   156.7606
		Eccentricity    0.09
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "23 UMa B"
{
	ParentBody "23 UMa"
	Class      "K7 V"
	AppMagn    9.19
	MassSol    0.63
	Orbit
	{
		Period          7900
		SemiMajorAxis   373.2394
		Eccentricity    0.09
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//55 UMa;6thCVB, spanish wiki


Barycenter "55 UMa A"
{
	ParentBody "55 UMa"
	Orbit
	{
		Period          5.1307
		SemiMajorAxis   2.0064
		Eccentricity    0.126
		Inclination     64.8
		AscendingNode   130
		ArgOfPericenter 43.9
		Epoch           2448805
		MeanAnomaly     0
	}
}

Star "55 UMa Aa/HIP 55266/HD 98353"
{
	ParentBody "55 UMa A"
	Class      "A3 V"
	AppMagn    5.3
	MassSol    2.4
	Orbit
	{
		Period          0.00707418
		SemiMajorAxis   0.0265
		Inclination     64.8 //unknown, IN and AN just aligned
		AscendingNode   130
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "55 UMa Ab"
{
	ParentBody "55 UMa A"
	Class      "A7 V"
	MassSol    1.9
	Orbit
	{
		Period          0.00707418
		SemiMajorAxis   0.0335
		Inclination     64.8 //unknown, IN and AN just aligned
		AscendingNode   130
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "55 UMa B"
{
	ParentBody "55 UMa"
	Class      "A3 V"
	AppMagn    5.9
	MassSol    2.4
	Orbit
	{
		Period          5.1307
		SemiMajorAxis   3.5948
		Eccentricity    0.126
		Inclination     64.8
		AscendingNode   130
		ArgOfPericenter 223.9
		Epoch           2448805
		MeanAnomaly     0
	}
}

//78 UMa; 6thCVB, spanish wiki

Star "78 UMa A/HIP 63503/HD 113139"
{
	ParentBody "78 UMa"
	Class      "F2 V"
	Radius     1044000
	AppMagn    5.02
	MassSol    1.5
	Orbit
	{
		Period          104
		SemiMajorAxis   12.0655
		Eccentricity    0.416
		Inclination     49.5
		AscendingNode   93.2
		ArgOfPericenter 112.7
		Epoch           2422814.582042
		MeanAnomaly     0
	}
}

Star "78 UMa B"
{
	ParentBody "78 UMa"
	Class      "G6 V"
	Radius     626400
	AppMagn    7.88
	MassSol    1
	Orbit
	{
		Period          104
		SemiMajorAxis   18.0983
		Eccentricity    0.416
		Inclination     49.5
		AscendingNode   93.2
		ArgOfPericenter 292.7
		Epoch           2422814.582042
		MeanAnomaly     0
	}
}

//HD 89744; with exoplanet

//AW UMa; spanish wiki
//in 6thCVB but rejected
//data non related for a very close binary (almost contacting)

Star "AW UMa A/HIP 56109/HD 99946"
{
	ParentBody "AW UMa"
	Class      "F0 V"
	Radius     1113600
	AppMagn    6.83
	MassSol    1.6
	Orbit
	{
		Period          0.00120192
		SemiMajorAxis   0.0012
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "AW UMa B"
{
	ParentBody "AW UMa"
	Class      "F2 V"
	MassSol    0.16
	Orbit
	{
		Period          0.00120192
		SemiMajorAxis   0.0124
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//W UMa; eclipsing contact binary; eng wiki

Star "W UMa A/HIP 47727/HD 83950"
{
	ParentBody "W UMa"
	Class      "F8 V"
	Radius     754464
	AppMagn    7.75
	MassSol    1.19
	Orbit
	{
		Period          0.00091397
		SemiMajorAxis   0.0037
		Inclination     86
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "W UMa B"
{
	ParentBody "W UMa"
	Class      "F8 V"
	Radius     539400
	AppMagn    8.48
	MassSol    0.57
	Orbit
	{
		Period          0.00091397
		SemiMajorAxis   0.0077
		Inclination     86
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//65 UMa;SIMBAD

//Unique sextuple system: 65 UMa
//Authors: P. Zasche, R. Uhlґa?r, M. ?Slechta, M. Wolf, P. Harmanec, J.A. Nemravovґa, and D. Kor?cґakovґa
//Astronomy & Astrophysics manuscript 4 July, 2012

Barycenter "65 UMa (ABC)"
{
	ParentBody "65 UMa"
	Orbit
	{
		Period          591000
		SemiMajorAxis   2572.7213
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Barycenter "65 UMa (AB)"
{
	ParentBody "65 UMa (ABC)"
	Orbit
	{
		Period          11000
		SemiMajorAxis   143.2614
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Barycenter "65 UMa A"
{
	ParentBody "65 UMa (AB)"
	Orbit
	{
		Period          118.209
		SemiMajorAxis   12.7427
		Eccentricity    0.504
		Inclination     38.1
		AscendingNode   2.1
		ArgOfPericenter 22.7
		Epoch           2447515.9
		MeanAnomaly     0
	}
}

Barycenter "65 UMa Aa"
{
	ParentBody "65 UMa A"
	Orbit
	{
		Period          1.7563
		SemiMajorAxis   1.5288
		Eccentricity    0.169
		Inclination     47
		ArgOfPericenter 180
		Epoch           2449615.4
		MeanAnomaly     0
	}
}



Star "65 UMa Aa1/HIP 58112/HD 103483"
{
	ParentBody "65 UMa Aa"
	Class      "A7 V"
	MassSol    1.76
	Orbit
	{
		Period          0.0047
		SemiMajorAxis   0.0234
		Inclination     47
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "65 UMa Aa2"
{
	ParentBody "65 UMa Aa"
	Class      "A7 V"
	MassSol    1.76
	Orbit
	{
		Period          0.0047
		SemiMajorAxis   0.0234
		Inclination     47
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "65 UMa Ab"
{
	ParentBody "65 UMa A"
	Class      "A1 V"
	MassSol    2.4
	Orbit
	{
		Period          1.7563
		SemiMajorAxis   1.0424
		Eccentricity    0.169
		Inclination     47
		ArgOfPericenter 0
		Epoch           2449615.4
		MeanAnomaly     0
	}
}

Star "65 UMa B/DN UMa B"
{
	ParentBody "65 UMa (AB)"
	Class      "A V"
	AppMagn    9
	MassSol    2.1
	Orbit
	{
		Period          118.209
		SemiMajorAxis   35.9224
		Eccentricity    0.504
		Inclination     38.1
		AscendingNode   2.1
		ArgOfPericenter 202.7
		Epoch           2447515.9
		MeanAnomaly     0
	}
}

Star "65 UMa C/BD+47 1913 C"
{
	ParentBody "65 UMa (ABC)"
	Class      "A V"
	AppMagn    8.32
	Orbit
	{
		Period          11000
		SemiMajorAxis   604.714
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "65 UMa D"
{
	ParentBody "65 UMa"
	Class      "A2 V"
	Orbit
	{
		Period          591000
		SemiMajorAxis   12153.0455
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//TAU UMa;english wiki, 6thCVB

Star "TAU UMa A/HIP 45075/HD 78362"
{
	ParentBody "TAU UMa"
	Class      "F3 III"
	AppMagn    4.65
	Orbit
	{
		Period          2.9087
		SemiMajorAxis   0.0269
		Eccentricity    0.48
		Inclination     87.3
		AscendingNode   296.5
		ArgOfPericenter 349.4
		Epoch           2425721.6
		MeanAnomaly     0
	}
}

Star "TAU UMa B"
{
	ParentBody "TAU UMa"
	AppMagn    9 //Unknown, sp companion
	Orbit
	{
		Period          2.9087
		SemiMajorAxis   0.1078
		Eccentricity    0.48
		Inclination     87.3
		AscendingNode   296.5
		ArgOfPericenter 169.4
		Epoch           2425721.6
		MeanAnomaly     0
	}
}

Star	"Gliese 412 A/GJ 412 A/LTT 12976/LFT 757/LHS 38/NLTT 26245/FK5 4979/SAO 43609/HIP 54211"
{
	ParentBody	   "Gliese 412"
	Class		   "M2 V"
	MassSol			0.48
	Radius			333840
	AppMagn			8.68
	Orbit
	{
		SemiMajorAxis	32.75862
		Period          1200
		Eccentricity    0.32
		MeanAnomaly     0
		ArgOfPericen    180
	}
}

Star "Gliese 412 B/GJ 412 B/WX UMa/LTT 12977"
{
	ParentBody "Gliese 412"
	Class      "M6V"
	AppMagn		16.05
	MassSol     0.10
	Radius      90415
	Orbit
	{
		SemiMajorAxis	157.24138
		Period          1200
		Eccentricity    0.32
		MeanAnomaly     0
		ArgOfPericen    0

	}
}

////////////////////////////////END OF URSA MAJOR---//////////////////////////////

//////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////BOOTES////////////////////////////////////////
//////////////////////////////////////////////////////////////////////////////////

//Izar;

Star "Izar A/Pulcherrima/Mirac/EPS Boo A/HIP 72105/HD 129989/HR 5506/SAO 83500"
{
	ParentBody "Izar"
	Class      "K0 III"
	AppMagn    2.37
	Radius     23100000
	MassSol    4.6
	Orbit
	{
		Period          1000
		SemiMajorAxis   54.5812
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Izar B/EPS Boo B/HD 129988/HR 5505"
{
	ParentBody "Izar"
	Class      "A2 V"
	AppMagn    5.12
	MassSol    2.1
	Orbit
	{
		Period          1000
		SemiMajorAxis   119.5589
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//DEL Boo;

Star "DEL Boo A/HIP 74666/HD 135722"
{
	ParentBody "DEL Boo"
	Class      "G8 III"
	Radius     7350000
	AppMagn    3.56
	MassSol    3.16304
	Orbit
	{
		Period          76000
		SemiMajorAxis   745.9563
		Eccentricity    0.73
		Inclination     86
		AscendingNode   75
		ArgOfPericenter 218
		Epoch           16247838.107953
		MeanAnomaly     0
	}
}

Star "DEL Boo B"
{
	ParentBody "DEL Boo"
	Class      "G0 V"
	AppMagn    7.89
	MassSol    1.09
	Orbit
	{
		Period          76000
		SemiMajorAxis   2164.6715
		Eccentricity    0.73
		Inclination     86
		AscendingNode   75
		ArgOfPericenter 38
		Epoch           16247838.107953
		MeanAnomaly     0
	}
}

//NU2 Boo; spanish, english wiki, jim kaller website

Star "NU2 Boo A/HIP 76041/HD 138629"
{
	ParentBody "NU2 Boo"
	Class      "A5 V"
	Radius     2505600
	AppMagn    4.997
	MassSol    2.4
	Orbit
	{
		Period          8.48
		SemiMajorAxis   3.6
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "NU2 Boo B"
{
	ParentBody "NU2 Boo"
	Class      "A5 V"
	MassSol    2.4
	Orbit
	{
		Period          8.48
		SemiMajorAxis   3.6
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


//44 Boo; AB Orbit from 6thCVB, other data english wiki
//Good system

Barycenter "44 Boo B"
{
	ParentBody "44 Boo"
	Orbit
	{
		Period          209.8
		SemiMajorAxis   17.8059
		Eccentricity    0.5111
		Inclination     83.55
		AscendingNode   57.14
		ArgOfPericenter 219.86
		Epoch           2455942.049471
		MeanAnomaly     0
	}
}


Star "44 Boo A/HIP 73695/HD 133640"
{
	ParentBody "44 Boo"
	Class      "F V"
	Radius     765600
	AppMagn    5.2
	MassSol    1.1
	Orbit
	{
		Period          209.8
		SemiMajorAxis   28.975
		Eccentricity    0.5111
		Inclination     83.55
		AscendingNode   57.14
		ArgOfPericenter 39.86
		Epoch           2455942.049471
		MeanAnomaly     0
	}
}

Star "44 Boo Ba"
{
	ParentBody "44 Boo B"
	Class      "G0 V"
	Radius     623000
	MassSol    1
	Orbit
	{
		Period          0.0007
		SemiMajorAxis   0.0044
		Inclination     83.55   //unknown, just aligned with rest of the system
		AscendingNode   57.14
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}


Star "44 Boo Bb"
{
	ParentBody "44 Boo B"
	Class      "K V"  //generic related with its mass    
	Radius     462000
	MassSol    0.79
	Orbit
	{
		Period          0.0007
		SemiMajorAxis   0.0055
		Inclination     83.55
		AscendingNode   57.14
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//Asellus Secondus;

Star "Asellus Secondus A/IOT Boo A/HIP 69713/HD 125161" 
{
	ParentBody "Asellus Secondus"
	Class      "A9 V"
	AppMagn    4.75
	MassSol    2.3  
	Orbit
	{
		Period          18486.8472
		SemiMajorAxis   547.4827
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Asellus Secondus B/IOT Boo B"
{
	ParentBody "Asellus Secondus"
	Class      "A2 V"
	AppMagn    8.27
	MassSol    2.1
	Orbit
	{
		Period          18486.8472
		SemiMajorAxis   599.624
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//KAP Boo;

Star "KAP1 Boo/KAP Boo A/HIP 69483/HD 124674"
{
	ParentBody "KAP Boo"
	Class      "F1 V"
	AppMagn    4.54
	MassSol    1.5
	Orbit
	{
		Period          6675
		SemiMajorAxis   312.4443
		Eccentricity    0.5
		Inclination     99.5
		AscendingNode   53.2
		ArgOfPericenter 25.1
		Epoch           2400068.7
		MeanAnomaly     0
	}
}

Star "KAP2 Boo/KAP Boo B/HIP 69481/HD 124675"
{
	ParentBody "KAP Boo"
	Class      "A8 V"    //itself binary?, strange mass  for companion                 
	AppMagn    6.62   
	MassSol    2.05
	Orbit
	{
		Period          6675
		SemiMajorAxis   228.9097
		Eccentricity    0.5
		Inclination     99.5
		AscendingNode   53.2
		ArgOfPericenter 205.1
		Epoch           2400068.7
		MeanAnomaly     0
	}
}

//Alkalurops (MU1 Boo, MU2 Boo); data from 6thCVB and wiki
//Good system

Barycenter "MU 1 Boo"
{
	ParentBody "Alkalurops"
	Orbit
	{
		Period          107365.4756
		SemiMajorAxis   1486.2217
		Inclination     130.016
		AscendingNode   130.04
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "MU1 Boo A/HIP 75411/HD 137391"
{
	ParentBody "MU 1 Boo"
	Class      "F0 V"
	Radius     1330000
	AppMagn    4.31
	MassSol    1.7
	Orbit
	{
		Period          3.748
		SemiMajorAxis   1.832
		Eccentricity    0.27194
		Inclination     130.016
		AscendingNode   130.04
		ArgOfPericenter 44.204
		Epoch           2453855.92
		MeanAnomaly     0
	}
}

Star "MU1 Boo B"
{
	ParentBody "MU 1 Boo"
	Class      "F V" 	//Unknown, related with mass
	MassSol    1.7 		//with data of 6thCVB total Mass of the pair is 3.41l 
	Orbit				//so with 3rd kepler law companion must have around 1.7 MassSol too.
	{
		Period          3.748
		SemiMajorAxis   1.832
		Eccentricity    0.27194
		Inclination     130.016
		AscendingNode   130.04
		ArgOfPericenter 224.204
		Epoch           2453855.92
		MeanAnomaly     0
	}
}

Barycenter "MU2 Boo"
{
	ParentBody "Alkalurops"
	Orbit
	{
		Period          107365.4756
		SemiMajorAxis   2489.2384
		Inclination     130.016
		AscendingNode   130.04
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "MU2 Boo A/HIP 75415/HD 137392/SAO 646867/HR 5734"
{
	ParentBody "MU2 Boo"
	Class      "G1 V"
	AppMagn    7.09
	MassSol    1.07
	Orbit
	{
		Period          256.5
		SemiMajorAxis   25.4199
		Eccentricity    0.579
		Inclination     134.2
		AscendingNode   176.2
		ArgOfPericenter 338.7
		Epoch           2401962.904914
		MeanAnomaly     0
	}
}

Star "MU2 Boo B"
{
	ParentBody "MU2 Boo"
	Class      "G5 V"
	AppMagn    7.63
	MassSol    0.96
	Orbit
	{
		Period          256.5
		SemiMajorAxis   28.3325
		Eccentricity    0.579
		Inclination     134.2
		AscendingNode   176.2
		ArgOfPericenter 158.7
		Epoch           2401962.904914
		MeanAnomaly     0
	}
}

//KSI Boo;

Star "KSI Boo A/HIP 72659/HD 131156"
{
	ParentBody "KSI Boo"
	Class      "G8 V"
	Radius     581000
	AppMagn    4.76
	MassSol    0.9
	Orbit
	{
		Period          151.6
		SemiMajorAxis   14.0164
		Eccentricity    0.51
		Inclination     139
		AscendingNode   347
		ArgOfPericenter 203
		Epoch           2418417.065969
		MeanAnomaly     0
	}
}

Star "KSI Boo B"
{
	ParentBody "KSI Boo"
	Class      "K4 V"
	Radius     427000
	AppMagn    6.95
	MassSol    0.66
	Orbit
	{
		Period          151.6
		SemiMajorAxis   19.1133
		Eccentricity    0.51
		Inclination     139
		AscendingNode   347
		ArgOfPericenter 23
		Epoch           2418417.065969
		MeanAnomaly     0
	}
}

//ZET Boo;

Star "ZET Boo A/HIP 71795/HD 129246"
{
	ParentBody "ZET Boo"
	Class      "A2 III"
	AppMagn    4.46
	Orbit
	{
		Period          124.5479
		SemiMajorAxis   63.4184
		Eccentricity    0.9977
		Inclination     102.3
		AscendingNode   8.2
		ArgOfPericenter 262.9
		Epoch           2460183
		MeanAnomaly     0
	}
}

Star "ZET Boo B"
{
	ParentBody "ZET Boo"
	Class      "A2 III"
	AppMagn    4.55
	Orbit
	{
		Period          124.5479
		SemiMajorAxis   63.4184
		Eccentricity    0.9977
		Inclination     102.3
		AscendingNode   8.2
		ArgOfPericenter 82.9
		Epoch           2460183
		MeanAnomaly     0
	}
}

//ETA Boo;6thCVB, spanish wiki


Star "Muphrid A/HIP 67927/HD 121370"
{
	ParentBody "Muphrid"
	Class      "G0 IV"
	Radius     1879200
	AppMagn    2.68
	MassSol    1.65
	Orbit
	{
		Period          1.354
		SemiMajorAxis   0.0234
		Eccentricity    0.26
		Inclination     115.7
		AscendingNode   75.2
		ArgOfPericenter 326.3
		Epoch           2428136.2
		MeanAnomaly     0
	}
}

Star "Muphrid B"
{
	ParentBody "Muphrid"
	AppMagn    5 					//unknown,sp companion
	Orbit
	{
		Period          1.354
		SemiMajorAxis   0.3855
		Eccentricity    0.26
		Inclination     115.7
		AscendingNode   75.2
		ArgOfPericenter 146.3
		Epoch           2428136.2
		MeanAnomaly     0
	}
}

//PI Boo; english and spanish wiki

Star "PI1 Boo/HIP 71762/HD 129174/HR 5475"
{
	ParentBody "PI Boo"
	Class      "B9 V"
	Radius     1879200
	AppMagn    4.91
	MassSol    3.4
	Orbit
	{
		Period          4989.856765
		SemiMajorAxis   210.356797
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "PI2 Boo/HD 129175/HR 5476"
{
	ParentBody "PI Boo"
	Class      "A6 V"
	Radius     2088000
	AppMagn    5.82
	MassSol    2.3
	Orbit
	{
		Period          4989.856765
		SemiMajorAxis   310.962222
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//TAU Boo;with exoplanet

//TET Boo;english and spanish wiki

Star "Asellus Primus A/TET Boo A/HIP 70497/HD 126660"
{
	ParentBody "Asellus Primus"
	Class      "F7 V"
	Radius     1183200
	AppMagn    4.04
	MassSol    1.35
	Orbit
	{
		Period          23375.536575
		SemiMajorAxis   271.091859
		ArgOfPericenter 0
 
		MeanAnomaly     0
	}
}

Star "TET Boo B/TET Boo B"
{
	ParentBody "Asellus Primus"
	Class      "M2 V"
	AppMagn    11.1

	Orbit
	{
		Period          23375.536575
		SemiMajorAxis   731.948019
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


//6 Boo;6thCVB, english wiki

Star "6 Boo A/HIP 67480/HD 120539"
{
	ParentBody "6 Boo"
	Class      "K4 III"
	AppMagn    4.93
	MassSol    5
	Orbit
	{
		Period          3.1973
		SemiMajorAxis   0.5418
		Eccentricity    0.38
		Inclination     134
		AscendingNode   90
		ArgOfPericenter 111
		Epoch           2449112
		MeanAnomaly     0
	}
}

Star "6 Boo B"
{
	ParentBody "6 Boo"
	AppMagn    10 			//unknown, SP companion
	Orbit
	{
		Period          3.1973
		SemiMajorAxis   0.5418
		Eccentricity    0.38
		Inclination     134
		AscendingNode   90
		ArgOfPericenter 291
		Epoch           2449112
		MeanAnomaly     0
	}
}

//12 Boo;6thCVB, spanish wiki

Star "12 Boo A/HIP 69226/HD 123999"
{
	ParentBody "12 Boo"
	Class      "F9 IV"
	Radius     1719120
	AppMagn    4.82
	MassSol    1.416
	Orbit
	{
		Period          0.0263
		SemiMajorAxis   0.0629
		Eccentricity    0.19214
		Inclination     107.95
		AscendingNode   80.49
		ArgOfPericenter 286.832
		Epoch           2454099.93572
		MeanAnomaly     0
	}
}

Star "12 Boo B"
{
	ParentBody "12 Boo"
	Class      "F8 IV"
	Radius     956304
	MassSol    1.37
	Orbit
	{
		Period          0.0263
		SemiMajorAxis   0.0648
		Eccentricity    0.19214
		Inclination     107.95
		AscendingNode   80.49
		ArgOfPericenter 106.832
		Epoch           2454099.93572
		MeanAnomaly     0
	}
}

//A Boo;6thCVB, english wiki

Star "A Boo A/HIP 69879/HD 125351"
{
	ParentBody "A Boo"
	Class      "K0 III"
	AppMagn    4.81
	MassSol    1
	Orbit
	{
		Period          0.5811
		SemiMajorAxis   0.0776
		Eccentricity    0.57
		Inclination     83.5
		AscendingNode   195.2
		ArgOfPericenter 224.9
		Epoch           2440286.002
		MeanAnomaly     0
	}
}

Star "A Boo B"
{
	ParentBody "A Boo"
	AppMagn    10 				//unknown, spectroscopic binary
	Orbit
	{
		Period          0.5811
		SemiMajorAxis   0.0776
		Eccentricity    0.57
		Inclination     83.5
		AscendingNode   195.2
		ArgOfPericenter 44.9
		Epoch           2440286.002
		MeanAnomaly     0
	}
}

//DE Boo;6thCVB, spanish wiki
//using wiki semimajor axis valor
//more according system Mass-period


Star "DE Boo A/HIP 72848/HD 131511"
{
	ParentBody "DE Boo"
	Class      "K2 V"
	Radius     577680
	AppMagn    6.01
	MassSol    0.93
	Orbit
	{
		Period          0.3436
		SemiMajorAxis   0.1696
		Eccentricity    0.51
		Inclination     93.4
		AscendingNode   248.3
		ArgOfPericenter 219
		Epoch           2450203.4
		MeanAnomaly     0
	}
}

Star "DE Boo B"
{
	ParentBody "DE Boo"
	Class      "M V" 			//unknown,related with Mass, could be also a white dwarf
	MassSol    0.45
	Orbit
	{
		Period          0.3436
		SemiMajorAxis   0.3504
		Eccentricity    0.51
		Inclination     93.4
		AscendingNode   248.3
		ArgOfPericenter 39
		Epoch           2450203.4
		MeanAnomaly     0
	}
}

//46 Boo;6thCVB, SIMBAD

Star "46 Boo A/HIP 74087/HD 134320"
{
	ParentBody "46 Boo"
	Class      "K2 III"
	AppMagn    5.68
	Orbit
	{
		Period          7.0332
		SemiMajorAxis   0.8881
		Eccentricity    0.83
		Inclination     62
		AscendingNode   82.6
		ArgOfPericenter 175.3
		Epoch           2448356.6
		MeanAnomaly     0
	}
}

Star "46 Boo B"
{
	ParentBody "46 Boo"
	AppMagn    12 //unknown,sp companion
	Orbit
	{
		Period          7.0332
		SemiMajorAxis   0.8881
		Eccentricity    0.83
		Inclination     62
		AscendingNode   82.6
		ArgOfPericenter 355.3
		Epoch           2448356.6
		MeanAnomaly     0
	}
}

//AD Boo;spanish wiki
//eclipsing binary

Star "AD Boo A"
{
	ParentBody "AD Boo"
	Class      "F4 V"
	Radius     1120560
	AbsMagn    3.12
	MassSol    1.41
	Orbit
	{
		Period          0.005671
		SemiMajorAxis   0.020236
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "AD Boo B"
{
	ParentBody "AD Boo"
	Class      "F8 V"
	Radius     842160
	AbsMagn    4.06
	MassSol    1.21
	Orbit
	{
		Period          0.005671
		SemiMajorAxis   0.023581
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//AR Boo;spanish wiki
//contact binary


Star "AR Boo A"
{
	ParentBody "AR Boo"
	Class      "G9 V"
	Radius     452400
	AbsMagn    5.93
	MassSol    0.35
	Orbit
	{
		Period          0.000945
		SemiMajorAxis   0.007464
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "AR Boo B"
{
	ParentBody "AR Boo"
	Class      "K1 V"
	Radius     696000
	AbsMagn    5.23
	MassSol    0.9
	Orbit
	{
		Period          0.000945
		SemiMajorAxis   0.002903
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


//GU Boo;spanish wiki
//eclipsing binary


Star "GU Boo A"
{
	ParentBody "GU Boo"
	Class      "M1 V"
	Radius     438480
	AbsMagn    8.6
	MassSol    0.61
	Orbit
	{
		Period          0.001339
		SemiMajorAxis   0.006416
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "GU Boo B"
{
	ParentBody "GU Boo"
	Class      "M V"
	Radius     431520
	AbsMagn    8.89
	MassSol    0.6
	Orbit
	{
		Period          0.001339
		SemiMajorAxis   0.006522
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//GV Boo;spanish wiki
//eclipsing binary

Star "CV Boo A"
{
	ParentBody "CV Boo"
	Class      "G3 V"
	Radius     876960
	AbsMagn    4.32
	MassSol    1.03
	Orbit
	{
		Period          0.002321
		SemiMajorAxis   0.009177
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "CV Boo B"
{
	ParentBody "CV Boo"
	Class      "G5 V"
	Radius     814320
	AbsMagn    4.57
	MassSol    0.97
	Orbit
	{
		Period          0.002321
		SemiMajorAxis   0.009744
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

////////////////////////////////CYGNUS///////////////////////////////

Barycenter	"Albireo A/BET1 Cyg/6 Cyg A/HIP 95947/HR 7417/SAO 87301"
{
	ParentBody	"Albireo"
	Mass        8.2

	Orbit
	{
		Period			100000
		Eccentricity    0.413	// approx. match components coords
		ArgOfPericenter 240		// approx. match components coords
		MeanAnomaly     0
	}
}

Star	"Albireo Aa/HD 183912"
{
	ParentBody	"Albireo A"
	Class       "K3II"
	AppMagn     3.18
	MassSol     5
	RadiusSol   70
	Teff        4080
	Luminosity  1200

	Orbit
	{
		Epoch           2450812.22279
		Period          213.859
		SemiMajorAxis   24.725	// mass ratio * 63.357
		Eccentricity    0.256
		Inclination     154.9
		AscendingNode   170.4
		ArgOfPericenter 219.4
		MeanAnomaly     0
	}
}

Star	"Albireo Ac/HD 183913"
{
	ParentBody	"Albireo A"
	Class       "B8V"
	AppMagn     5.82
	MassSol     3.2
	RadiusSol   3.5
	Teff        12000
	Luminosity  230

	Orbit
	{
		Epoch           2450812.22279
		Period          213.859
		SemiMajorAxis   38.632	// mass ratio * 63.357
		Eccentricity    0.256
		Inclination     154.9
		AscendingNode   170.4
		ArgOfPericenter 39.4
		MeanAnomaly     0
	}
}

Star	"Albireo B/BET2 Cyg/6 Cyg B/HD 183914/HIP 95951"
{
	ParentBody	"Albireo"
	Class       "B8V"
	AppMagn     5.11718744
	MassSol     3.3
	RadiusSol   3.1
	Teff        13200
	Luminosity  190

	Orbit
	{
		Period			100000
		Eccentricity    0.413	// approx. match components coords
		ArgOfPericenter 60 		// approx. match components coords
		MeanAnomaly     0
	}
}

Barycenter	"V1581 Cyg (AC)/Gliese 1245 (AC)/GJ 1245 (AC)"
{
	ParentBody	   "Gliese 1245"
	Orbit
	{
		SemiMajorAxis	11
		ArgOfPericenter	0
		MeanAnomaly		0
		Period			128.705		//calculated by SE
		Eccentricity	0.38
	}
}

Star	"V1581 Cyg A/Gliese 1245 A/GJ 1245 A"
{
	ParentBody	   "Gliese 1245 (AC)"
	Class		   "M5.5 V"
	AppMagn			13.41
	MassSol			0.08035		//calculated by SE
	Orbit
	{
		SemiMajorAxis	1
		ArgOfPericenter	0
		MeanAnomaly		0
		Period			2.495		//calculated by SE
		Eccentricity	0.38
	}
}

Star	"V1581 Cyg C/Gliese 1245 C/GJ 1245 C"
{
	ParentBody	   "Gliese 1245 (AC)"
	Class		   "M5.5 V"
	AppMagn			16.75
	MassSol			0.08035		//calculated by SE
	Orbit
	{
		SemiMajorAxis	1
		ArgOfPericenter	180
		MeanAnomaly		0
		Period			2.495		//calculated by SE
		Eccentricity	0.38
	}
}

Star	"V1581 Cyg B/Gliese 1245 B/GJ 1245 B"
{
	ParentBody	   "Gliese 1245"
	Class		   "M6 V"
	AppMagn			14.01
	MassSol			0.08035		//calculated by SE
	Orbit
	{
		SemiMajorAxis	22
		ArgOfPericenter	180
		MeanAnomaly		0
		Period			128.705		//calculated by SE
		Eccentricity	0.38
	}
}

Star	"61 Cyg A/Gliese 820 A/Struve 2758 A/HD 201091/HIP 104214/HR 8085"
{
	ParentBody	   "61 Cyg"
	Class		   "K5 V"
	AppMagn			5.21
	MassSol			0.7
	Radius			462507.5
	FeH			   -0.2

	RotationPeriod	848.88

	Orbit
	{
		SemiMajorAxis	11.497263158
		Period			678
		Eccentricity	0.49
		Inclination		51
		AscendingNode	178
		ArgOfPericenter	149
		MeanAnomaly		0
		Epoch			1709
	}
}

Star	"61 Cyg B/Gliese 820 B/Struve 2758 B/HD 201092/HIP 104217/HR 8086"
{
	ParentBody	   "61 Cyg"
	Class		   "K7 V"
	MassSol			0.63
	Radius			413822.5
	FeH			   -0.27

	RotationPeriod	908.16

	Orbit
	{
		SemiMajorAxis	12.774736842
		Period			678
		Eccentricity	0.49
		Inclination		51
		AscendingNode	178
		ArgOfPericenter	329
		MeanAnomaly		0
		Epoch			1709
	}
}

//OMI1 Cyg; spanish wiki, prof. Jim Kaler

Star "OMI1 Cyg A/HIP 56243/HD 100261"
{
	ParentBody "OMI1 Cyg"
	Class      "K2 II"
	Radius     69600000
	AppMagn    3.96
	MassSol    5
	Orbit
	{
		Period          10.36
		SemiMajorAxis   6.2174
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "OMI1 Cyg B"
{
	ParentBody "OMI1 Cyg"
	Class      "B3 V"
	MassSol    6.5
	Orbit
	{
		Period          10.36
		SemiMajorAxis   4.7826
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


//OMI2 Cyg;

Star "OMI2 Cyg A/HIP 99848/HD 192909"
{
	ParentBody "OMI2 Cyg"
	Class      "K4 Ib"
	Radius     128800000
	AppMagn    4.02
	MassSol    7.45
	Orbit
	{
		Period          3.1439
		SemiMajorAxis   0.4361
		Eccentricity    0.304
		Inclination     57.1
		AscendingNode   48
		ArgOfPericenter 221.4
		Epoch           2452646.9
		MeanAnomaly     0
	}
}

Star "OMI2 Cyg B/HD 192910"
{
	ParentBody "OMI2 Cyg"
	Class      "B7 IV"
	Radius     2100000
	MassSol    4.13
	Orbit
	{
		Period          3.1439
		SemiMajorAxis   0.7868
		Eccentricity    0.304
		Inclination     57.1
		AscendingNode   48
		ArgOfPericenter 41.4
		Epoch           2452646.9
		MeanAnomaly     0
	}
}

//DEL Cyg;6thCVB,eng & sp wiki

Star "DEL Cyg A/HIP 97165/HD 186882"
{
	ParentBody "DEL Cyg"
	Class      "B9.5 IV"
 
	AppMagn    2.89
	MassSol    3.15
	Orbit
	{
		Period          918.1
		SemiMajorAxis   60.215
		Eccentricity    0.52
		Inclination     146.9
		AscendingNode   95.7
		ArgOfPericenter 129.7
		Epoch           2409359.059439
		MeanAnomaly     0
	}
}

Star "DEL Cyg B"
{
	ParentBody "DEL Cyg"
	Class      "F1 V"
 
	AppMagn    6.27
	MassSol    1.6
	Orbit
	{
		Period          918.1
		SemiMajorAxis   118.5482
		Eccentricity    0.52
		Inclination     146.9
		AscendingNode   95.7
		ArgOfPericenter 309.7
		Epoch           2409359.059439
		MeanAnomaly     0
	}
}

//ETA Cyg;sp wiki

Star "ETA Cyg A/HIP 98110/HD 188947"
{
	ParentBody "ETA Cyg"
	Class      "K0 III"
	Radius     7308000
	AppMagn    3.88
	MassSol    2.5
	Orbit
	{
		Period          3500
		SemiMajorAxis   54.1667
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "ETA Cyg B"
{
	ParentBody "ETA Cyg"
	Class      "M V"
	AbsMagn    12
	Orbit
	{
		Period          3500
		SemiMajorAxis   270.8333
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//EPS Cyg;eng,sp wiki

Star "EPS Cyg A/HIP 102488/HD 197989"
{
	ParentBody "EPS Cyg"
	Class      "K0 III"
	Radius     7530720
	AppMagn    2.5
	MassSol    2
	Orbit
	{
		Period          47876.60606722
		SemiMajorAxis   226.8845
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "EPS Cyg B"
{
	ParentBody "EPS Cyg"
	Class      "M3 V"
	AppMagn    13.4
	Orbit
	{
		Period          47876.60606722
		SemiMajorAxis   1512.5634
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//LAM Cyg;6thCVB, spanish wiki
//very good system


Barycenter "LAM Cyg A"
{
	ParentBody "LAM Cyg"
	Orbit
	{
		Period          391.3
		SemiMajorAxis   54.9859
		Eccentricity    0.45
		Inclination     133.8
		AscendingNode   138.6
		ArgOfPericenter 298.4
		Epoch           2376669.882648
		MeanAnomaly     0
	}
}

Star "LAM Cyg Aa/HIP 102589/HD 198185"
{
	ParentBody "LAM Cyg A"
	Class      "B5 V"
	Radius     3410400
	AppMagn    5.4
	MassSol    5.5
	Orbit
	{
		Period          11.63
		SemiMajorAxis   5.3918
		Eccentricity    0.524
		Inclination     135
		AscendingNode   150
		ArgOfPericenter 272
		Epoch           2445039.569838
		MeanAnomaly     0
	}
}

Star "LAM Cyg Ab"
{
	ParentBody "LAM Cyg A"
	Class      "B5 V"
	AppMagn    5.8
	MassSol    5
	Orbit
	{
		Period          11.63
		SemiMajorAxis   5.9309
		Eccentricity    0.524
		Inclination     135
		AscendingNode   150
		ArgOfPericenter 92
		Epoch           2445039.569838
		MeanAnomaly     0
	}
}

Star "LAM Cyg B"
{
	ParentBody "LAM Cyg"
	Class      "B7 V"
	AppMagn    6.06
	Orbit
	{
		Period          391.3
		SemiMajorAxis   128.3003
		Eccentricity    0.45
		Inclination     133.8
		AscendingNode   138.6
		ArgOfPericenter 118.4
		Epoch           2376669.882648
		MeanAnomaly     0
	}
}

//OME1 Cyg;sp wiki, prof jim kaler

Barycenter "OME1 Cyg (AB)"
{
	ParentBody "OME1 Cyg"
	Orbit
	{
		Period          597759.37
		SemiMajorAxis   2935.5684
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "OME1 Cyg A/HIP 101138/HD 195556"
{
	ParentBody "OME1 Cyg (AB)"
	Class      "B2.5 IV"
	Radius     4732800
	AppMagn    4.94
	MassSol    7.65
	Orbit
	{
		Period          110939.06
		SemiMajorAxis   548.601
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "OME1 Cyg B"
{
	ParentBody "OME1 Cyg (AB)"
	Class      "G V"
	AppMagn    12.9
	Orbit
	{
		Period          110939.06
		SemiMajorAxis   4196.7978
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "OME1 Cyg C"
{
	ParentBody "OME1 Cyg"
	Class      "A V"
	AppMagn    9.4
	Orbit
	{
		Period          597759.37
		SemiMajorAxis   12696.3334
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//TAU Cyg;6thCVB for AB Orbit, spanish wiki, prof. jim kaler


Barycenter "TAU Cyg (ABC)"
{
	ParentBody "TAU Cyg"
	Orbit
	{
		Period          664796.40146852
		SemiMajorAxis   282.8205
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Barycenter "TAU Cyg (AB)"
{
	ParentBody "TAU Cyg (ABC)"
	Orbit
	{
		Period          46464.58
		SemiMajorAxis   244.5223
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}



Star "TAU Cyg A/HIP 104887/HD 202444"
{
	ParentBody "TAU Cyg (AB)"
	Class      "F2 IV"
	Radius     1726080
	AppMagn    3.84
	MassSol    1.65
	Orbit
	{
		Period          49.65863014
		SemiMajorAxis   7.3407
		Eccentricity    0.2392
		Inclination     134.44
		AscendingNode   339.75
		ArgOfPericenter 298.77
		Epoch           2447553
		MeanAnomaly     0
	}
}

Star "TAU Cyg B"
{
	ParentBody "TAU Cyg (AB)"
	Class      "G0 V"
	Radius     647280
	AppMagn    6.44
	MassSol    1.03
	Orbit
	{
		Period          49.65863014
		SemiMajorAxis   11.7594
		Eccentricity    0.2392
		Inclination     134.44
		AscendingNode   339.75
		ArgOfPericenter 118.77
		Epoch           2447553
		MeanAnomaly     0
	}
}

Star "TAU Cyg F"
{
	ParentBody "TAU Cyg (ABC)"
	Class      "M2.5 V"
	AppMagn    12
	MassSol    0.4
	Orbit
	{
		Period          46464.58
		SemiMajorAxis   1638.2997
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "TAU Cyg I/2MASS J21153159+3804391"
{
	ParentBody "TAU Cyg"
	Class      "M8 V"
	AppMagn    16
	MassSol    0.08
	Orbit
	{
		Period          664796.40146852
		SemiMajorAxis   10888.5905
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}


//ZET Cyg;WD

//52 Cyg;sp wiki

Star "52 Cyg A/HIP 102453/HD 197912"
{
	ParentBody "52 Cyg"
	Class      "G9.5III"
	Radius     10509600
	AppMagn    4.22
	MassSol    2.75
	Orbit
	{
		Period          3732.4934332
		SemiMajorAxis   99.6319
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "52 Cyg B"
{
	ParentBody "52 Cyg"
	Class      "G V"
	AppMagn    8.7
	MassSol    1
	Orbit
	{
		Period          3732.4934332
		SemiMajorAxis   273.9877
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//A36;sp wiki
//contact binary

Star "A36 A"
{
	ParentBody "A36"
	Class      "B0 Ib"
	Radius     7203600 //unknown, typical
	AppMagn    11.4
	MassSol    19.8
	Orbit
	{
		Period          0.00319777
		SemiMajorAxis   0.0288
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "A36 B"
{
	ParentBody "A36"
	Class      "B0 III" 
	Radius     5046000 //unknown, typical
	MassSol    13.8
	Orbit
	{
		Period          0.00319777
		SemiMajorAxis   0.0413
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//CI Cyg;WD STAR PRESENT
//Gliese 777;with exoplanets
//HD 188753;6thCVB, eng, sp wiki

Barycenter "HD 188753 (BC)"
{
	ParentBody "HD 188753"
	Orbit
	{
		Period          25.7
		SemiMajorAxis   7.5781
		Eccentricity    0.5
		Inclination     34
		AscendingNode   43
		ArgOfPericenter 236
		Epoch           2447234.675452
		MeanAnomaly     0
	}
}

Star "HD 188753 A/HIP 98001"
{
	ParentBody "HD 188753"
	Class      "G8 V"
	Radius     890880
	AppMagn    7.43
	MassSol    1.06
	Orbit
	{
		Period          25.7
		SemiMajorAxis   4.9281
		Eccentricity    0.5
		Inclination     34
		AscendingNode   43
		ArgOfPericenter 56
		Epoch           2447234.675452
		MeanAnomaly     0
	}
}

Star "HD 188753 B"
{
	ParentBody "HD 188753 (BC)"
	Class      "K0 V"
	MassSol    0.96
	Orbit
	{
		Period          156
		SemiMajorAxis   0.2713
		Inclination     34 //IN and RA just aligned with A
		AscendingNode   43
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "HD 188753 C"
{
	ParentBody "HD 188753 (BC)"
	Class      "M V"
	MassSol    0.67
	Orbit
	{
		Period          156
		SemiMajorAxis   0.3887
		Inclination     34
		AscendingNode   43
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//59 Cyg;6thCVB,sp wiki, prof jim kaler



Barycenter "59 Cyg (AB)"
{
	ParentBody "59 Cyg"
	Orbit
	{
		Period          194562.864
		SemiMajorAxis   1468.2567
		Inclination     145.8
		AscendingNode   205.2
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Barycenter "59 Cyg A"
{
	ParentBody "59 Cyg (AB)"
	Orbit
	{
		Period          161.5
		SemiMajorAxis   53.871
		Eccentricity    0.261
		Inclination     145.8
		AscendingNode   205.2
		ArgOfPericenter 85.5
		Epoch           2460419.918828
		MeanAnomaly     0
	}
}



Star "59 Cyg Aa/HIP 103632/HD 200120"
{
	ParentBody "59 Cyg A"
	Class      "B1.5 V"
	Radius     6612000
	AppMagn    4.74
	MassSol    8
	Orbit
	{
		Period          0.0773
		SemiMajorAxis   0.0352
		Inclination     145.8
		AscendingNode   205.2
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "59 Cyg Ab"
{
	ParentBody "59 Cyg A"
	Class      "B VI"
	Radius     139200
	MassSol    0.8
	Orbit
	{
		Period          0.0773
		SemiMajorAxis   0.3519
		Inclination     145.8
		AscendingNode   205.2
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "59 Cyg B"
{
	ParentBody "59 Cyg (AB)"
	Class      "B V"
	AppMagn    7.6
	MassSol    6
	Orbit
	{
		Period          161.5
		SemiMajorAxis   36.7302
		Eccentricity    0.261
		Inclination     145.8
		AscendingNode   205.2
		ArgOfPericenter 265.5
		Epoch           2460419.918828
		MeanAnomaly     0
	}
}

Star "59 Cyg C"
{
	ParentBody "59 Cyg"
	Class      "A0 V"
	AppMagn    9.4
	MassSol    3
	Orbit
	{
		Period          194562.864
		SemiMajorAxis   7243.3997
		Inclination     145.8
		AscendingNode   205.2
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

//SS Cyg;WD STAR PRESENT

//V444 Cyg;sp wiki

Star "V444 Cyg A/HIP 100214/HD 193576"
{
	ParentBody "V444 Cyg"
	Class      "O6 III"
	Radius     6960000
	AppMagn    7.94
	MassSol    25
	Orbit
	{
		Period          0.01123288
		SemiMajorAxis   0.0514
		Eccentricity    0.3
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "V444 Cyg B"
{
	ParentBody "V444 Cyg"
	Class      "WN5"
	Radius     2088000
	MassSol    10
	Orbit
	{
		Period          0.01123288
		SemiMajorAxis   0.1286
		Eccentricity    0.3
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//V478;sp wiki
//eclipsing binary

Star "V478 Cyg A/HIP 100227/HD 193611"
{
	ParentBody "V478 Cyg"
	Class      "O9 V"
	Radius     5171280
	AppMagn    8.66
	MassSol    16.62
	Orbit
	{
		Period          0.0078926
		SemiMajorAxis   0.0544
		Eccentricity    0.019
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "V478 Cyg B"
{
	ParentBody "V478 Cyg"
	Class      "O9 V"
	Radius     5171280
	MassSol    16.27
	Orbit
	{
		Period          0.0078926
		SemiMajorAxis   0.0556
		Eccentricity    0.019
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//V1687 Cyg;spanish wiki

Star "V1687 Cyg A/HD 193793"
{
	ParentBody "V1687 Cyg"
	Class      "O5 V"
	Radius     16773600
	AppMagn    6.89
	MassSol    54
	Orbit
	{
		Period          7.9
		SemiMajorAxis   4.4983
		Eccentricity    0.88
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "V1687 Cyg B"
{
	ParentBody "V1687 Cyg"
	Class      "WC7"
	Radius     7864800
	MassSol    20
	Orbit
	{
		Period          7.9
		SemiMajorAxis   12.1454
		Eccentricity    0.88
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


////////////////////////////////////SCORPIUS///////////////////////////////////////////////////

Star "Antares A/Cor Scorpii/Vespertilio/Scorpion Heart/ALF Sco A/HIP 80763/HD 148478"
{
	ParentBody "Antares"
	Class      "M1 Ia"
	AppMagn    0.96
	MassSol    12.4
	Orbit
	{
		Period          1217.536
		SemiMajorAxis   220.2038
		Eccentricity    0.0786
		Inclination     80.75
		AscendingNode   90.99
		ArgOfPericenter 0.01
		Epoch           2186258.167158
		MeanAnomaly     0
	}
}

Star "Antares B/ALF Sco B"
{
	ParentBody "Antares"
	Class      "B2 V"
	AppMagn    5.4
	MassSol    10
	Orbit
	{
		Period          1217.536
		SemiMajorAxis   273.0527
		Eccentricity    0.0786
		Inclination     80.75
		AscendingNode   90.99
		ArgOfPericenter 180.01
		Epoch           2186258.167158
		MeanAnomaly     0
	}
}

//Graffias;english wiki, 6thCVB

Barycenter "BET1 Sco/BD-19 4307/HD 144217/HIP 78820/HR 5984"
{
	ParentBody "Graffias"
	Orbit
	{
		Period          10852.8017
		SemiMajorAxis   643.6148
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Barycenter "BET2 Sco/BD-19 4308/HD 144218/HIP 78821/HR 5985"
{
	ParentBody "Graffias"
	Orbit
	{
		Period          10852.8017
		SemiMajorAxis   1010.7776
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "BET Sco A"
{
	ParentBody "BET1 Sco"
	Class      "B0 V"
	AppMagn    2.62
	Radius     13300000
	MassSol    10
	Orbit
	{
		Period          610
		SemiMajorAxis   238.9678
		Eccentricity    0.909
		Inclination     87.1
		AscendingNode   89.5
		ArgOfPericenter 282.9
		Epoch           2626860.788813
		MeanAnomaly     0
	}
}

Barycenter "BET Sco B"
{
	ParentBody "BET1 Sco" 
	Orbit
	{
		Period          610
		SemiMajorAxis   238.9678
		Eccentricity    0.909
		Inclination     87.1
		AscendingNode   89.5
		ArgOfPericenter 102.9
		Epoch           2626860.788813
		MeanAnomaly     0
	}
}

Star "BET Sco Ba"
{
	ParentBody "BET Sco B"
	Class      "B0 V"
	AppMagn    2.62
	Orbit
	{
		Period          0.0187
		SemiMajorAxis   0.087
		Eccentricity    0.291
		Inclination     111.8
		AscendingNode   294.2
		ArgOfPericenter 54.8
		Epoch           2449788.509
		MeanAnomaly     0
	}
}

Star "BET Sco Bb"
{
	ParentBody "BET Sco B"
	Class      "B0 V"
	Orbit
	{
		Period          0.0187
		SemiMajorAxis   0.087
		Eccentricity    0.291
		Inclination     111.8
		AscendingNode   294.2
		ArgOfPericenter 234.8
		Epoch           2449788.509
		MeanAnomaly     0
	}
}

Star "BET Sco C"
{
	ParentBody "BET2 Sco"
	Class      "B2 V"
	AppMagn    4.52
	Orbit
	{
		Period          38.77
		SemiMajorAxis   5.0653
		Eccentricity    0.025
		Inclination     41.8
		AscendingNode   5.4
		ArgOfPericenter 155.8
		Epoch           2449338.470517
		MeanAnomaly     0
	}
}

Barycenter "BET Sco E"
{
	ParentBody "BET2 Sco"
	Orbit
	{
		Period          38.77
		SemiMajorAxis   10.1306
		Eccentricity    0.025
		Inclination     41.8
		AscendingNode   5.4
		ArgOfPericenter 335.8
		Epoch           2449338.470517
		MeanAnomaly     0
	}
}

Star "BET Sco Ea"
{
	ParentBody "BET Sco E"
	Class      "B2 V"
	Orbit
	{
		Period          0.029315068
		Inclination     41.8
		AscendingNode   5.4
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "BET Sco Eb"
{
	ParentBody "BET Sco E"
	Class      "B2 V"
	Orbit
	{
		Period          0.029315068
		Inclination     41.8
		AscendingNode   5.4
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//Dschuba;english wiki

Barycenter "DEL Sco (AB)"
{
	ParentBody "Dschubba"
	Orbit
	{
		Period          10.8093
		SemiMajorAxis   2.8493
		Eccentricity    0.9373
		Inclination     34.12
		AscendingNode   175
		ArgOfPericenter 359.5
		Epoch           2455745.29
		MeanAnomaly     0
	}
}

Star "Dschubba A/Dzuba/Iclarcrau/DEL Sco A/HIP 78401/HD 143275"
{
	ParentBody "DEL Sco (AB)"
	Class      "B0 IV"
	AppMagn    2.39
	MassSol    15 
	Orbit
	{
		Period          0.0548
		SemiMajorAxis   0.0975
		Inclination     34.12
		AscendingNode   175
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Dschubba B/DEL Sco B"
{
	ParentBody "DEL Sco (AB)"
	Class      "B V"
	MassSol    5 //unknwown, generic for a projected distance of 58M and period
	Orbit  //of 20 days results with 3rd kepler law around 20 total     MassSol    for AB
	{
		Period          0.0548
		SemiMajorAxis   0.2925
		Inclination     34.12
		AscendingNode   175
		ArgOfPericenter 180
		MeanAnomaly     0
	}

}

Star "DEL Sco C"
{
	ParentBody "Dschubba"
	Class      "B V"  //unknown related with Mass
	AppMagn    4.62
	Orbit
	{
		Period          10.8093
		SemiMajorAxis   11.3974
		Eccentricity    0.9373
		Inclination     34.12
		AscendingNode   175
		ArgOfPericenter 179.5
		Epoch           2455745.29
		MeanAnomaly     0
	}
}

//Shaula;

Star "Shaula Aa/LAM Sco Aa/HIP 85927/HD 158926"
{
	ParentBody "LAM Sco A"
	Class      "B2 IV"
	Radius     4550000
	AppMagn    1.6
	MassSol    10.4 //confirmed
	Orbit
	{
		Period          0.016438356
		SemiMajorAxis   0.021986152
		Inclination     77.2
		AscendingNode   271.3
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "LAM Sco Ab"
{
	ParentBody "LAM Sco A"
	AppMagn    14.9
	Class      "A V"
	MassSol    1.8 //confirmed
	Orbit
	{
		Period          0.016438356
		SemiMajorAxis   0.127031103
		Inclination     77.2
		AscendingNode   271.3
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Barycenter "LAM Sco A"
{
	ParentBody "Shaula"
	Orbit
	{
		Period          2.8844
		SemiMajorAxis   3.4352
		Eccentricity    0.121
		Inclination     77.2
		AscendingNode   271.3
		ArgOfPericenter 74.8
		Epoch           2451562.3
		MeanAnomaly     0
	}
}

Star "LAM Sco B"
{
	ParentBody "Shaula"
	Class      "B V"
	AppMagn    12
	MassSol    8.1
	Orbit
	{
		Period          2.8844
		SemiMajorAxis   5.1741
		Eccentricity    0.121
		Inclination     77.2
		AscendingNode   271.3
		ArgOfPericenter 254.8
		Epoch           2451562.3
		MeanAnomaly     0
	}
}


//Jabbah, source wikipedia, system made with only apparent separations

Barycenter "NU Sco (AB)"
{
	ParentBody "Jabbah"
	Orbit
	{
		Period          81185.52
		SemiMajorAxis   1493.0659
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Barycenter "NU Sco (CD)"
{
	ParentBody "Jabbah"
	Orbit
	{
		Period          81185.52
		SemiMajorAxis   3996.1469
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Barycenter "NU Sco A"
{
	ParentBody "NU Sco (AB)"
	Orbit
	{
		Period          537.2199
		SemiMajorAxis   70.7669
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}


Star "Jabbah Aa/NU Sco Aa/HIP 79374/HD 145501"
{
	ParentBody "NU Sco A"
	Class      "B2 IV"
	AppMagn    4.4
	Orbit
	{
		Period          0.0024
		SemiMajorAxis   0.0127
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "NU Sco Ab"
{
	ParentBody "NU Sco A"
	Class      "B V"
	Orbit
	{
		Period          0.0024
		SemiMajorAxis   0.0275
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "NU Sco B"
{
	ParentBody "NU Sco (AB)"
	Class      "B2 IV"
	AppMagn    6.9
	MassSol    7.4
	Orbit
	{
		Period          537.2199
		SemiMajorAxis   103.2813
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "NU Sco C"
{
	ParentBody "NU Sco (CD)"
	Class      "B8 V"
	AppMagn    6.5
	Orbit
	{
		Period          2085
		SemiMajorAxis   160.6599
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "NU Sco D"
{
	ParentBody "NU Sco (CD)"
	Class      "B9 V"
	AppMagn    6.9
	Orbit
	{
		Period          2085
		SemiMajorAxis   160.6599
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//pi sco, triple, 3rd component unknown     Class

Barycenter "PI Sco (AB)"
{
	ParentBody "PI Sco"
	Orbit
	{
		Period          188267
		SemiMajorAxis   326.6705
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "PI Sco A/HIP 78265/HD 143018"
{
	ParentBody "PI Sco (AB)"
	Class      "B1 V"
	Radius     3500000
	AppMagn    2.89
	MassSol    12.5
	Orbit
	{
		Period          0.0043
		SemiMajorAxis   0.026
		Inclination     42
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "PI Sco B"
{
	ParentBody "PI Sco (AB)"
	Class      "B2 V"
	Radius     2800000
	Orbit
	{
		Period          0.0043
		SemiMajorAxis   0.044
		Inclination     42
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "PI Sco C"
{
	ParentBody "PI Sco"
	Class      "K V" //unknown related with     AbsMagn
	AppMagn    12.2
	Orbit
	{
		Period          188267
		SemiMajorAxis   8711.2141
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


//Al Niyat
//fifth faint component, unknown class     

Barycenter "SIG Sco (ACB)"
{
	ParentBody "Al Niyat"
	Orbit
	{
		Period          30930.42
		SemiMajorAxis   229.9093
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Barycenter "SIG Sco (AC)"
{
	ParentBody "SIG Sco (ACB)"
	Orbit
	{
		Period          205
		SemiMajorAxis   32.1951
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Al Niyat Aa/SIG Sco Aa/HIP 80112/HD 147165"
{
	ParentBody "SIG Sco (AC)"
	Class      "B1 III"
	Radius     8890000 //confirmed
	AppMagn    3.3
	MassSol    18
	Orbit
	{
		Period          0.0904
		SemiMajorAxis   0.252
		Eccentricity    0.322
		Inclination     158.2
		AscendingNode   104
		ArgOfPericenter 283
		Epoch           2434889
		MeanAnomaly     0
	}
}

Star "SIG Sco Ab"
{
	ParentBody "SIG Sco (AC)"
	Class      "O9 V"   //    Class      in Spanish wiki
	Radius     7700000 //confirmed
	AppMagn    4.1
	MassSol    12
	Orbit
	{
		Period          0.0904
		SemiMajorAxis   0.3779
		Eccentricity    0.322
		Inclination      158.2
		AscendingNode   104
		ArgOfPericenter 103
		Epoch           2434889
		MeanAnomaly     0
	}
}


Star "SIG Sco C"
{
	ParentBody "SIG Sco (ACB)"
	Class      "B1 V"
	AppMagn    5.2
	MassSol    11
	Orbit
	{
		Period          205
		SemiMajorAxis   87.8049
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


Star "SIG Sco B"
{
	ParentBody "Al Niyat"
	Class      "B9 V"
	AppMagn    8.7
	MassSol    2.9
	Orbit
	{
		Period          30930.42
		SemiMajorAxis   3250.4422
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//KSI Sco
//Spectral types from www.perezmedia.net/beltofvenus/archives/001390.html//


Barycenter "KSI Sco (ABC)"
{
	ParentBody "KSI Sco"
	Orbit
	{
		Period          307341.75
		SemiMajorAxis   2666.6667
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Barycenter "KSI Sco DE"
{
	ParentBody "KSI Sco"
	Orbit
	{
		Period          307341.75
		SemiMajorAxis   5333.3333
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Barycenter "KSI Sco (AB)"
{
	ParentBody "KSI Sco (ABC)"
	Orbit
	{
		Period          1514.43
		SemiMajorAxis   73.8671
		Eccentricity    0.041
		Inclination     131.5
		AscendingNode   47.4
		ArgOfPericenter 59.3
		Epoch           2534125.794542
		MeanAnomaly     0
	}
}


Star "KSI Sco A/HIP 78727/HD 144069"
{
	ParentBody "KSI Sco (AB)"
	Class      "F5 IV"
	AppMagn    5.16
	Orbit
	{
		Period          45.9
		SemiMajorAxis   8.2976
		Eccentricity    0.744
		Inclination     34.5
		AscendingNode   25.3
		ArgOfPericenter 163.8
		Epoch           2450529.160085
		MeanAnomaly     0
	}
}

Star "KSI Sco B"
{
	ParentBody "KSI Sco (AB)"
	Class      "G1 V"
	AppMagn    4.87
	Orbit
	{
		Period          45.9
		SemiMajorAxis   10.2362
		Eccentricity    0.744
		Inclination     34.5
		AscendingNode   25.3
		ArgOfPericenter 343.8
		Epoch           2450529.160085
		MeanAnomaly     0
	}
}

Star "KSI Sco C"
{
	ParentBody "KSI Sco (ABC)"
	Class      "G1 V"
	AppMagn    7.3
	MassSol    1.21
	Orbit
	{
		Period          1514.43
		SemiMajorAxis   145.9029
		Eccentricity    0.041
		Inclination     131.5
		AscendingNode   47.4
		ArgOfPericenter 239.3
		Epoch           2534125.794542
		MeanAnomaly     0
	}
}

Star "KSI Sco D"
{
	ParentBody "KSI Sco DE"
	Class      "G8 V"
	AppMagn    7.5
	Orbit
	{
		Period          4376.97
		SemiMajorAxis   155.7078
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "KSI Sco E"
{
	ParentBody "KSI Sco DE"
	Class      "K1 V"
	AppMagn    8.1
	Orbit
	{
		Period          4376.97
		SemiMajorAxis   170.1922
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star	"GJ 2130 A/HIP 86961"
{
	ParentBody  "GJ 2130"
	Class       "M2V"
	AbsMagn     11.53
	MassSol     0.30

	Orbit
	{
		Period         250		// calculated
		SemiMajorAxis  34.05	// 65 * mass ratio
		Eccentricity   0.2		// random value
		Inclination    20		// random value
		ArgOfPericen   0		// random value
		MeanAnomaly    0		// random value
	}
}

Barycenter	"GJ 2130 (BC)"
{
	ParentBody  "GJ 2130"
	MassSol     0.33

	Orbit
	{
		Period         250		// calculated
		SemiMajorAxis  30.95	// 65 * mass ratio
		Eccentricity   0.2		// random value
		Inclination    20		// random value
		ArgOfPericen   180		// random value
		MeanAnomaly    0		// random value
	}
}

Star	"GJ 2130 B/HIP 86963"
{
	ParentBody  "GJ 2130 (BC)"
	Class       "M2V"
	AbsMagn     12.79
	MassSol     0.18

	Orbit
	{
		Period         10.7	// calculated
		SemiMajorAxis  2.81	// 6.18 * mass ratio
		Eccentricity   0.5	// random value
		Inclination    30	// random value
		ArgOfPericen   60	// random value
		MeanAnomaly    0	// random value
	}
}

Star	"GJ 2130 C"
{
	ParentBody  "GJ 2130 (BC)"
	Class       "M2V"
	AbsMagn     13.79
	MassSol     0.15

	Orbit
	{
		Period         10.7	// calculated
		SemiMajorAxis  3.37 // 6.18 * mass ratio
		Eccentricity   0.5	// random value
		Inclination    30	// random value
		ArgOfPericen   240	// random value
		MeanAnomaly    0	// random value
	}
}

//////////////////////CENTAURUS/////////////////////////////////////

Star	"ALF Cen A/Toliman A/Bungula A/Rigel Kentaurus A/Gliese 559 A/HD 128620/HIP 71683"
{
	ParentBody	"Toliman"
	Class		"G2V"
	AppMagn     0.01
	Radius      853992
	MassSol     1.09
	Age         6

	RotationPeriod  923.6
	Obliquity       82
	EqAscendNode    67.726

	Orbit
	{
		Period			79.914
		SemiMajorAxis	10.765   // mass ratio 1.09:0.92
		Eccentricity	0.5179
		Inclination		82.986
		AscendingNode	67.726
		ArgOfPericenter 3.772
		MeanAnomaly		200.119
	}
}

Star	"ALF Cen B/Toliman B/Bungula B/Rigel Kentaurus B/Gliese 559 B/HD 128621/HIP 71681"
{
	ParentBody	"Toliman"
	Class		"K0V"
	AppMagn     1.34
	Radius      602040
	MassSol     0.92
	Age         6

	RotationPeriod  850.5
	Obliquity       83
	EqAscendNode    67.726

	Orbit
	{
		Period			79.914
		SemiMajorAxis	12.755   // mass ratio 1.09:0.92
		Eccentricity	0.5179
		Inclination		82.986
		AscendingNode	67.726
		ArgOfPericenter	183.772
		MeanAnomaly		200.119
	}
}

//BET CEN
//3rd component, unknown     Class

Star "Agena Aa/BET Cen Aa/HIP 68702/HD 122451"
{
	ParentBody "Agena"
	Class      "B1 III"
	AppMagn    1.29
	MassSol    10.7
	Orbit
	{
		Period          0.9781
		SemiMajorAxis   1.3316
		Eccentricity    0.824
		Inclination     67.4
		AscendingNode   288.3
		ArgOfPericenter 241.3
		Epoch           2451600
		MeanAnomaly     0
	}
}

Star "Agena Ab/BET Cen Ab"
{
	ParentBody "Agena"
	Class      "B1 III"
	AppMagn    1.44
	MassSol    10.3
	Orbit
	{
		Period          0.9781
		SemiMajorAxis   1.3834
		Eccentricity    0.824
		Inclination     67.4
		AscendingNode   288.3
		ArgOfPericenter 61.3
		Epoch           2451600
		MeanAnomaly     0
	}
}

//DEL Cen; eng and sp wiki

Star "DEL Cen A/HIP 59196/HD 105435"
{
	ParentBody "DEL Cen"
	Class      "B2 IV"
	Radius     4663200
	AppMagn    2.58
	MassSol    8.6
	Orbit
	{
		Period          4.81778643
		SemiMajorAxis   2.6915
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "DEL Cen B"
{
	ParentBody "DEL Cen"
	Class      "B4 V" //if it's in the main sequence
	MassSol    5.5 //between 4 and 7 sm
	Orbit
	{
		Period          4.81778643 //unknown
		SemiMajorAxis   4.2085
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//EPS Cen; eng and sp wiki, prof. jim kaller
//unconfirmed bounded binary

Star "EPS Cen A/HIP 66657/HD 118716"
{
	ParentBody "EPS Cen"
	Class      "B1 III"
	Radius     4350000
	AppMagn    2.29
	MassSol    11
	Orbit
	{
		Period          90071.43141115
		SemiMajorAxis   235.1386
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "EPS Cen B"
{
	ParentBody "EPS Cen"
	Class      "K V" //unknown, related with absmag
	AppMagn    13
	Orbit
	{
		Period          90071.43141115
		SemiMajorAxis   4310.8737
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//GAM Cen; 6thCVB, eng and sp wiki
//very good system

Star "Muhlifain A/GAM Cen A/HIP 61932/HD 110304"
{
	ParentBody "Muhlifain"
	Class      "A1 IV"
	AppMagn    2.82
	MassSol    2.8
	Orbit
	{
		Period          84.494
		SemiMajorAxis   18.6626
		Eccentricity    0.791
		Inclination     113.5
		AscendingNode   2.4
		ArgOfPericenter 187.2
		Epoch           2426420.983513
		MeanAnomaly     0
	}
}

Star "Muhlifain B/GAM Cen B"
{
	ParentBody "Muhlifain"
	Class      "A1 IV"
	AppMagn    2.88
	MassSol    2.8
	Orbit
	{
		Period          84.494
		SemiMajorAxis   18.6626
		Eccentricity    0.791
		Inclination     113.5
		AscendingNode   2.4
		ArgOfPericenter 7.2
		Epoch           2426420.983513
		MeanAnomaly     0
	}
}

//KAP Cen; eng and sp wiki
//good system description aside from the     Orbits

Barycenter "KAP Cen A"
{
	ParentBody "KAP Cen"
	Orbit
	{
		Period          3450
		SemiMajorAxis   29.375
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "KAP Cen Aa/HIP 73334/HD 132200"
{
	ParentBody "KAP Cen A"
	Class      "B2 IV"
	Radius     3480000
	AppMagn    3.13
	MassSol    7.5
	Orbit
	{
		Period          12.4
		SemiMajorAxis   3.3567
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "KAP Cen Ab"
{
	ParentBody "KAP Cen A"
	Class      "A0 V"
	Radius     3549600
	MassSol    3
	Orbit
	{
		Period          12.4
		SemiMajorAxis   8.3918
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "KAP Cen B"
{
	ParentBody "KAP Cen"
	Class      "K2 V"
	AppMagn    11
	Orbit
	{
		Period          3450
		SemiMajorAxis   440.625
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//LAM Cen; eng, sp wiki, prof jim kaler

Star "LAM Cen A/HIP 56561/HD 100841"
{
	ParentBody "LAM Cen"
	Class      "B9 III"
	Radius     6960000
	AppMagn    3.12
	MassSol    4.5
	Orbit
	{
		Period          358.05133818
		SemiMajorAxis   28.9382
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "LAM Cen B"
{
	ParentBody "LAM Cen"
	Class      "A V"
	AppMagn    6.8
	MassSol    2
	Orbit
	{
		Period          358.05133818
		SemiMajorAxis   65.1109
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//NU Cen;eng, sp wiki, prof. jim kaler
//spectroscopic binary

Star "NU Cen A/HIP 67464/HD 120307"
{
	ParentBody "NU Cen"
	Class      "B2 IV"
	Radius     4454400
	AppMagn    3.41
	MassSol    8.5
	Orbit
	{
		Period          0.00719178
		SemiMajorAxis   0.0018
		Eccentricity    0 //confirmed
		ArgOfPericenter 0
		Epoch           2450894.32 //confirmed
		MeanAnomaly     0
	}
}

Star "NU Cen B"
{
	ParentBody "NU Cen"
	AppMagn    7  //unknown, sp companion
	Orbit
	{
		Period          0.00719178 //confirmed
		SemiMajorAxis   0.0748 //for a low Mass star companion
		Eccentricity    0 //confirmed
		ArgOfPericenter 180
		Epoch           2450894.32
		MeanAnomaly     0
	}
}

//PSI Cen; spanish wiki
//well studied system
//eclipsing binary

Star "PSI Cen A/HIP 70090/HD 125473"
{
	ParentBody "PSI Cen"
	Class      "A0 IV"
	Radius     2575200
	AppMagn    4.05
	MassSol    3.1
	Orbit
	{
		Period          0.10635616
		SemiMajorAxis   0.1514
		Eccentricity    0.55
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "PSI Cen B"
{
	ParentBody "PSI Cen"
	Class      "A2 V"
	Radius     1252800
	MassSol    2
	Orbit
	{
		Period          0.10635616
		SemiMajorAxis   0.2347
		Eccentricity    0.55
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//KSI2 Cen; spanish wiki
//I used different Mass standars for Aa Ab for     Orbit dist.
//they seem more according for the 41000y     Orbit for B component

Barycenter "KSI2 Cen A"
{
	ParentBody "KSI2 Cen"
	Orbit
	{
		Period          41000
		SemiMajorAxis   159.2561
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "KSI2 Cen Aa/HIP 64004/HD 113791"
{
	ParentBody "KSI2 Cen A"
	Class      "B1 V"
	Radius     2923200
	AppMagn    4.26
	Orbit
	{
		Period          0.02095808
		SemiMajorAxis   0.1023
		Eccentricity    0.35
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "KSI2 Cen Ab"
{
	ParentBody "KSI2 Cen A"
	Class      "B V"
	Orbit
	{
		Period          0.02095808
		SemiMajorAxis   0.1231
		Eccentricity    0.35
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "KSI2 Cen B"
{
	ParentBody "KSI2 Cen"
	Class      "F7 V"
	AppMagn    9.41
	MassSol    1.21
	Orbit
	{
		Period          41000
		SemiMajorAxis   3422.0322
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//ZET Cen; english wiki
//good description for spectroscopic binary

Star "ZET Cen A/HIP 68002/HD 121263"
{
	ParentBody "ZET Cen"
	Class      "B2 V"
	Radius     4036800
	AppMagn    2.55
	MassSol    7.8
	Orbit
	{
		Period          0.00828493
		SemiMajorAxis   0.0234
		Eccentricity    0.5
		ArgOfPericenter 290
		Epoch           2413719.321
		MeanAnomaly     0
	}
}

Star "ZET Cen B"
{
	ParentBody "ZET Cen"
	Class      "F V"
	Orbit
	{
		Period          0.00828493
		SemiMajorAxis   0.1406
		Eccentricity    0.5
		ArgOfPericenter 110
		Epoch           2413719.321
		MeanAnomaly     0
	}
}

//1 Cen; sp wiki
//spectroscopic binary


Star "1 Cen A/HIP 67153/HD 119756"
{
	ParentBody "1 Cen"
	Class      "F2 V"
	Radius     904800
	AppMagn    4.23
	MassSol    1.43
	Orbit
	{
		Period          0.02724658
		SemiMajorAxis   0.0055
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "1 Cen B"
{
	ParentBody "1 Cen"
	Class      "M V"
	Radius     278400 //standard for M Class low MassSol star
	MassSol    0.08
	Orbit
	{
		Period          0.02724658
		SemiMajorAxis   0.0983
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//3 Cen; eng, sp wiki, prof. jim kaler

Star "3 Cen A/HIP 67669/HD 120709/HR 5210"
{
	ParentBody "3 Cen"
	Class      "B5 V"
	Radius     2018400
	AppMagn    4.53
	MassSol    5.1
	Orbit
	{
		Period          8738.37014716
		SemiMajorAxis   315.3829
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "3 Cen B/HR 5211"
{
	ParentBody "3 Cen"
	Class      "B8 V"
	Radius     1461600
	AppMagn    6.02
	MassSol    3
	Orbit
	{
		Period          8738.37014716
		SemiMajorAxis   536.1509
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//HD 113766; eng wiki

Star "HD 113766 A/HIP 63975"
{
	ParentBody "HD 113766"
	Class      "F3 V"
	AppMagn    7.56
	Orbit
	{
		Period          1302.70037138
		SemiMajorAxis   82.069
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "HD 113766 B"
{
	ParentBody "HD 113766"
	Class      "F5 V"
	Orbit
	{
		Period          1302.70037138
		SemiMajorAxis   87.931
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//HD 114729; with exoplanet

//HR 4523;with exoplanet

//RR Cen;sp wiki
//contact eclipsing binary

Star "RR Cen A/HIP 69779/HD 124689"
{
	ParentBody "RR Cen"
	Class      "F0 V"
	Radius     1419840
	AppMagn    7.45
	MassSol    1.85
	Orbit
	{
		Period          0.00165945
		SemiMajorAxis   0.0031
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "RR Cen B"
{
	ParentBody "RR Cen"
	Class      "F V"  //unknown,related with its Teff 7188K
	Radius     702960
	MassSol    0.39
	Orbit
	{
		Period          0.00165945
		SemiMajorAxis   0.0149
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//SS Cen

Star "SS Cen A/HD 114720"
{
	ParentBody "SS Cen"
	Class      "B8 V"
	Radius     1593840
	AppMagn    9.4
	MassSol    4
	Orbit
	{
		Period          0.00679096
		SemiMajorAxis   0.0123
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "SS Cen B"
{
	ParentBody "SS Cen"
	Class      "F2 V"
	Radius     2491680
	MassSol    1
	Orbit
	{
		Period          0.00679096
		SemiMajorAxis   0.049
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//SV Cen;sp wiki,eclipsing binary

Star "SV Cen A/HD 102552"
{
	ParentBody "SV Cen"
	Class      "B6 III"
	Radius     3480000 //unknown, generic radius too big in SE
	AppMagn    8.71
	Orbit
	{
		Period          0.00454515
		SemiMajorAxis   0.0371
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "SV Cen B"
{
	ParentBody "SV Cen"
	Class      "B2 V"
	Orbit
	{
		Period          0.00454515
		SemiMajorAxis   0.0442
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//SZ Cen;sp wiki

Star "SZ Cen A/HIP 67556/HD 120359"
{
	ParentBody "SZ Cen"
	Class      "A7 V"
	Radius     3201600
	AppMagn    8.89
	MassSol    2.31
	Orbit
	{
		Period          0.01125479
		SemiMajorAxis   0.0413
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "SZ Cen B"
{
	ParentBody "SZ Cen"
	Class      "A7 V"
	Radius     2505600
	MassSol    2.27
	Orbit
	{
		Period          0.01125479
		SemiMajorAxis   0.042
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//V636 Cen;sp wiki

Star "V636 Cen A/HIP 69781/HD 124784"
{
	ParentBody "V636 Cen"
	Class      "G1 V"
	Radius     709920
	AppMagn    8.7
	MassSol    1.05
	Orbit
	{
		Period          0.01173699
		SemiMajorAxis   0.0286
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "V636 Cen B"
{
	ParentBody "V636 Cen"
	Class      "K2 V"
	Radius     577680
	MassSol    0.85
	Orbit
	{
		Period          0.01173699
		SemiMajorAxis   0.0353
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//XX Cen;sp wiki

Star "XX Cen A/HIP 66696/HD 118769"
{
	ParentBody "XX Cen"
	Class      "F7 II"
	Radius     40368000
	AppMagn    7.82
	MassSol    3.3
	Orbit
	{
		Period          2.53178082
		SemiMajorAxis   1.7412
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "XX Cen B"
{
	ParentBody "XX Cen"
	AppMagn    16 //unknown
	Orbit
	{
		Period          2.53178082
		SemiMajorAxis   1.7412
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//M Cen;6thCVB, spanish wiki

Star "M Cen A/HIP 65387/HD 119834"
{
	ParentBody "M Cen"
	Class      "G8 III"
	AppMagn    4.64
	Orbit
	{
		Period          1.1973
		SemiMajorAxis   0.2572
		Eccentricity    0.13
		Inclination     48.2
		AscendingNode   280.3
		ArgOfPericenter 58.6
		Epoch           2424163
		MeanAnomaly     0
	}
}

Star "M Cen B"
{
	ParentBody "M Cen"
	AppMagn    9 //unknown
	Orbit
	{
		Period          1.1973
		SemiMajorAxis   0.2572
		Eccentricity    0.13
		Inclination     48.2
		AscendingNode   280.3
		ArgOfPericenter 238.6
		Epoch           2424163
		MeanAnomaly     0
	}
}



///////////////////URSA MINOR////////////////////////////////

Barycenter	"Polaris A/ALF UMi A"
{
	ParentBody	"Polaris"
	MassSol     5.76
	Orbit
	{
		Period			100000
		SemiMajorAxis	622	// mass ratio * 3200
		Eccentricity	0.6	// random
		ArgOfPericenter 0	// TODO
		MeanAnomaly     0
	}
}

Star	"Polaris Aa/ALF UMi Aa"
{
	ParentBody	"Polaris A"
	Class		"F7Ib"
	AppMagn     1.98
	Luminosity  2500
	RadSol      46
	MassSol     4.5
	Teff        6015
	FeH         0.049
	Age         0.7

	Orbit
	{
		Epoch           24001987.66
		Period			29.59
		SemiMajorAxis	4.05	// mass ratio * 18.5
		Eccentricity	0.608
		Inclination		130.2
		AscendingNode	167.1
		ArgOfPericenter 123.01
		MeanAnomaly     0
	}
}

Star	"Polaris Ab/ALF UMi Ab/Polaris P"
{
	ParentBody	"Polaris A"
	Class		"F6V"
	AppMagn     9.2
	Luminosity  3
	RadSol      1.04
	MassSol     1.26
	Age         0.7

	Orbit
	{
		Epoch           24001987.66
		Period			29.59
		SemiMajorAxis	14.45	// mass ratio * 18.5
		Eccentricity	0.608
		Inclination		130.2
		AscendingNode	167.1
		ArgOfPericenter 303.01
		MeanAnomaly     0
	}
}

Star	"Polaris B"
{
	ParentBody	"Polaris"
	Class		"F3V"
	AppMagn     8.7
	Luminosity  3.9
	RadSol      1.38
	MassSol     1.39
	Teff        6900
	Age         0.7

	Orbit
	{
		Period			100000
		SemiMajorAxis	2578	// mass ratio * 3200
		Eccentricity	0.6		// random
		ArgOfPericenter 180		// TODO
		MeanAnomaly     0
	}
}

//EPS UMi;sp wiki

Barycenter "EPS UMi A"
{
	ParentBody "EPS UMi"
	Orbit
	{
		Period          379922.98
		SemiMajorAxis   1725.4763
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

//spectroscopic binary

Star "EPS UMi Aa/HIP 82080/HD 153751"
{
	ParentBody "EPS UMi A"
	Class      "G5 III"
	AppMagn    4.21
	Orbit
	{
		Period          0.10816877 //only known period
		SemiMajorAxis   0.1639 //confirmed
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "EPS UMi Ab"
{
	ParentBody "EPS UMi A"
	AppMagn    8 //unknown
	Orbit
	{
		Period          0.10816877
		SemiMajorAxis   0.1639
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "EPS UMi B"
{
	ParentBody "EPS UMi"
	Class      "K0 V"
	AppMagn    11
	Orbit
	{
		Period          379922.98
		SemiMajorAxis   6470.536
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//LAM UMi; spanish wiki, prof. kim kaler
//known if both objects are bounded

Star "LAM UMi A/HIP 84535/HD 183030"
{
	ParentBody "LAM UMi"
	Class      "M1 III"
	Radius     39672000
	AppMagn    6.35
	MassSol    1.75
	Orbit
	{
		Period          1148843
		SemiMajorAxis   4222.6117
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "LAM UMi B"
{
	ParentBody "LAM UMi"
	Class      "K V"
	AppMagn    14
	Orbit
	{
		Period          1148843
		SemiMajorAxis   10556.5294
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//RR UMi;sp wiki
//spectroscopic binary, only known period comp.

Star "RR UMi A/HIP 73199/HD 132813"
{
	ParentBody "RR UMi"
	Class      "M4 III"
	Radius     87696000
	AppMagn    4.71
	MassSol    1.6
	Orbit
	{
		Period          2.05178082
		SemiMajorAxis   1.189
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "RR UMi B"
{
	ParentBody "RR UMi"
	AppMagn    9 					//unknown,SP companion
	Orbit
	{
		Period          2.05178082
		SemiMajorAxis   1.189
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//6thCVB;SIMBAD

Star "9 UMi A/HIP 73440/HD 133621"
{
	ParentBody "9 UMi"
	Class      "G0 IV" //most probably outside the main sequence
	AppMagn    6.66
	Orbit
	{
		Period          1.2791
		SemiMajorAxis   0.0465
		Eccentricity    0.217
		Inclination     51.9
		AscendingNode   307.5
		ArgOfPericenter 10
		Epoch           2447349
		MeanAnomaly     0
	}
}

Star "9 UMi B"
{
	ParentBody "9 UMi"
	AppMagn    13 //unknown,SP companion
	Orbit
	{
		Period          1.2791
		SemiMajorAxis   0.0931
		Eccentricity    0.217
		Inclination     51.9
		AscendingNode   307.5
		ArgOfPericenter 190
		Epoch           2447349
		MeanAnomaly     0
	}
}


///////////////////////////////ORION//////////////////////////////////


//Rigel;english wiki
//R2

Barycenter "Rigel B/BET Ori B"
{
	ParentBody "Rigel"
	Orbit
	{
		Period          20651.15422358
		SemiMajorAxis   1663.067
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Rigel A/BET Ori A/HIP 24436 A/HD 34085 A"
{
	ParentBody "Rigel"
	Class      "B8 Ia"
	Radius     54914400
	AppMagn    0.13
	MassSol    21
	Orbit
	{
		Period          20651.15422358
		SemiMajorAxis   536.933
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "Rigel Ba/BET Ori Ba"
{
	ParentBody "Rigel B"
	Class      "B9 V"
	AppMagn    6.67
	MassSol    3.84
	Orbit
	{
		Period          9.86
		SemiMajorAxis   0.0738
		Eccentricity    0.1
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Rigel Bb/BET Ori Bb"
{
	ParentBody "Rigel B"
	Class      "B9 V"
	MassSol    2.94
	Orbit
	{
		Period          9.86
		SemiMajorAxis   0.0965
		Eccentricity    0.1
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

///SIG ori;english, spanish, jim kaler website
// only sig ori A and B Orbits are stable C,D,E not

Star "SIG Ori A/HIP 26549/HD 37468"
{
	ParentBody "SIG Ori"
	Class      "O9 V"
	AppMagn    4.07
	MassSol    18
	Orbit
	{
		Period          156.7
		SemiMajorAxis   40.1952
		Eccentricity    0.0515
		Inclination     159.7
		AscendingNode   121.7
		ArgOfPericenter 8.7
		Epoch           2451361.912299
		MeanAnomaly     0
	}
}

Star "SIG Ori B"
{
	ParentBody "SIG Ori"
	Class      "B0 V"
	AppMagn    5.27
	MassSol    13.5
	Orbit
	{
		Period          156.7
		SemiMajorAxis   53.5935
		Eccentricity    0.0515
		Inclination     159.7
		AscendingNode   121.7
		ArgOfPericenter 188.7
		Epoch           2451361.912299
		MeanAnomaly     0
	}
}

Star "SIG Ori C"
{
	ParentBody "SIG Ori"
	Class      "A2 V"
	AppMagn    8.79
	Orbit
	{
		Period          35331 //unknown
		SemiMajorAxis   3900
		Inclination     159.7   //just aligned
		AscendingNode   121.7
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "SIG Ori D"
{
	ParentBody "SIG Ori"
	Class      "B2 V"
	AppMagn    6.62
	MassSol    7
	Orbit
	{
		Period          45259 //unknown
		SemiMajorAxis   4600
		Inclination     159.7
		AscendingNode   121.7
		ArgOfPericenter 0
		MeanAnomaly     90 //fictional
	}
}

Star "SIG Ori E"
{
	ParentBody "SIG Ori"
	Class      "B2 V"
	AppMagn    6.62
	MassSol    7
	Orbit
	{
		Period          266504 //unknown
		SemiMajorAxis   15000
		Inclination     159.7
		AscendingNode   121.7
		ArgOfPericenter 0
		MeanAnomaly     35 //fictional
	}
}

//Alnitak;english wiki
//very good system
//R2

Barycenter "Alnitak A"
{
	ParentBody "Alnitak"
	Orbit
	{
		Period          1408.6
		SemiMajorAxis   268.1234
		Eccentricity    0.07
		Inclination     72
		AscendingNode   155.5
		ArgOfPericenter 0
		Epoch           2402070.6
		MeanAnomaly     0
	}
}

Star "Alnitak Aa/HIP 26727 Aa/HD 37742 Aa"
{
	ParentBody "Alnitak A"
	Class      "O9.5 Ib"
	Radius     13920000
	AppMagn    2.08
	MassSol    33
	Orbit
	{
		Period          7.35660507
		SemiMajorAxis   4.1384
		Eccentricity    0.338
		Inclination     139.3
		AscendingNode   83.8
		ArgOfPericenter 0
		Epoch           2452734.2
		MeanAnomaly     0
	}
}

Star "Alnitak Ab"
{
	ParentBody "Alnitak A"
	Class      "B1 IV"
	Radius     5080800
	AppMagn    4.28
	MassSol    14
	Orbit
	{
		Period          7.35660507
		SemiMajorAxis   9.7549
		Eccentricity    0.338
		Inclination     139.3
		AscendingNode   83.8
		ArgOfPericenter 180
		Epoch           2452734.2
		MeanAnomaly     0
	}
}

Star "Alnitak B"
{
	ParentBody "Alnitak"
	Class      "B0 III"
	Radius     5011200
	AppMagn    4.01
	MassSol    16
	Orbit
	{
		Period          1408.6
		SemiMajorAxis   787.6126
		Eccentricity    0.07
		Inclination     72
		AscendingNode   155.5
		ArgOfPericenter 180
		Epoch           2402070.6
		MeanAnomaly     0
	}
}



//Mintaka;english wiki
//Mintaka B not confirmed to be bounded to the system
//R2

Barycenter "Mintaka A/DEL Ori A"
{
	ParentBody "Mintaka"
	Orbit
	{
		Period          326744.0527
		SemiMajorAxis   2695.9302
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Barycenter "Mintaka Aa/DEL Ori Aa/HD 36486"
{
	ParentBody "Mintaka A"
	Orbit
	{
		Period          126.4706
		SemiMajorAxis   56.4819
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "Mintaka Aa1/DEL Ori Aa1"
{
	ParentBody "Mintaka Aa"
	Class      "O 9.5 II"
	Radius     11484000
	AbsMagn    -5.4
	MassSol    24
	Orbit
	{
		Period          0.0157
		SemiMajorAxis   0.052
		Eccentricity    0.1133
		Inclination     76.5
		ArgOfPericenter 141.3
		Epoch           2456295.674
		MeanAnomaly     0
	}
}

Star "Mintaka Aa2/DEL Ori Aa2"
{
	ParentBody "Mintaka Aa"
	Class      "B1 V"
	Radius     4524000
	AbsMagn    -2.9
	MassSol    8.4
	Orbit
	{
		Period          0.0157
		SemiMajorAxis   0.1485
		Eccentricity    0.1133
		Inclination     76.5
		ArgOfPericenter 321.3
		Epoch           2456295.674
		MeanAnomaly     0
	}
}

Star "Mintaka Ab/DEL Ori Ab"
{
	ParentBody "Mintaka A"
	Class      "B0 IV"
	Radius     7238400
	AbsMagn    -4.2
	MassSol    22.5
	Orbit
	{
		Period          126.4706
		SemiMajorAxis   39.2236
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Barycenter "Mintaka C/DEL Ori C/HD 36485"
{
	ParentBody "Mintaka"
	Orbit
	{
		Period          326744.0527
		SemiMajorAxis   16445.1741
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Mintaka Ca/DEL Ori Ca"
{
	ParentBody "Mintaka C"
	Class      "B3 V"
	Radius     3967200
	AppMagn    6.85
	MassSol    9
	Orbit
	{
		Period          0.082136
		SemiMajorAxis   0.345884
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Mintaka Cb/DEL Ori Cb"
{
	ParentBody "Mintaka C"
	Class      "A V"
	MassSol    1.9
	Orbit
	{
		Period          0.082136
		SemiMajorAxis   0.345884
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


///Meissa, Meissa C F8V class but unknown separation

Star "Meissa A/HIP 26207/HD 36861"
{
	ParentBody "Meissa"
	Class      "O8 III"
	AppMagn    3.54
	MassSol    27.9
	Orbit
	{
		Period          8468.67
		SemiMajorAxis   576.8787
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Meissa B"
{
	ParentBody "Meissa"
	Class      "B0 V"
	AppMagn    5.61
	Orbit
	{
		Period          8468.67
		SemiMajorAxis   909.3172
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//22 Ori;spanish wiki

Star "22 Ori Aa/HIP 25044/HD 35039"
{
	ParentBody "22 Ori"
	Class      "B2 IV"
	AppMagn    4.7
	MassSol    8
	Orbit
	{
		Period          0.7205
		SemiMajorAxis   1.0139
		Eccentricity    0.15
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "22 Ori Ab"  //spectroscopic companion, spanish wiki
{
	ParentBody "22 Ori"
	AppMagn    9 //unknown
	Orbit
	{
		Period          0.7205  //confirmed period for spect companion
		SemiMajorAxis   1.0139
		Eccentricity    0.15
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//PHI1 Ori;

Star "PHI1 Ori A/HIP 26176/HD 36822"
{
	ParentBody "PHI1 Ori"
	Class      "B0 III"
	Radius     4830000
	AppMagn    4.4
	MassSol    14
	Orbit
	{
		Period          8.4
		SemiMajorAxis   6.2818
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "PHI1 Ori B" //spectroscopic companion, spanish wiki
{
	ParentBody "PHI1 Ori"
	AppMagn    9 //unknown
	Orbit
	{
		Period          8.4 //confirmed period for spect companion
		SemiMajorAxis   6.2818
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


//Hatysa;spanish and english wiki


Star "Hatsya A/HIP 26241/HD 37043"
{
	ParentBody "Hatsya"
	Class      "O9 III"
	Radius     5000000 // unknown
	AppMagn    2.77
	MassSol    15
	Orbit
	{
		Period          0.079764
		SemiMajorAxis   0.2275
		Eccentricity    0.764
		ArgOfPericenter 0
		Epoch           2450072.8
		MeanAnomaly     0
	}
}

Star "Hatsya B"
{
	ParentBody "Hatsya"
	Class      "B1 III"
	Radius     5000000
	Orbit
	{
		Period          0.079764
		SemiMajorAxis   0.2275
		Eccentricity    0.764
		ArgOfPericenter 180
		Epoch           2450072.8
		MeanAnomaly     0
	}
}


//MU Ori;6thCVB, english wiki, spanish wiki

Barycenter "MU Ori A/HIP 28614 A/HD 40932 A"
{
	ParentBody "MU Ori"
	Orbit
	{
		Period          18.644
		SemiMajorAxis   6.0271
		Eccentricity    0.7863
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Barycenter "MU Ori B/HIP 28614 B/HD 40932 B"
{
	ParentBody "MU Ori"
	Orbit
	{
		Period          18.644
		SemiMajorAxis   6.6729
		Eccentricity    0.7863
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


Star "MU Ori Aa"
{
	ParentBody "MU Ori A"
	Class      "A2 V"
	Radius     2030000
	AppMagn    4.3
	MassSol    2.1
	Orbit
	{
		Period          0.0122
		SemiMajorAxis   0.0255
		Eccentricity    0.0037
		Inclination     47.1
		AscendingNode   50.5
		ArgOfPericenter 304
		Epoch           2443739.69
		MeanAnomaly     0
	}
}

Star "MU Ori Ab"
{
	ParentBody "MU Ori A"
	Class      "G5 V"
	AppMagn    6.27
	MassSol    1
	Orbit
	{
		Period          0.0122
		SemiMajorAxis   0.0534
		Eccentricity    0.0037
		Inclination     47.1
		AscendingNode   50.5
		ArgOfPericenter 124
		Epoch           2443739.69
		MeanAnomaly     0
	}
}

Star "MU Ori Ba"
{
	ParentBody "MU Ori B"
	Class      "F5 V"
	Radius     910000
	AppMagn    4.3
	MassSol    1.4
	Orbit
	{
		Period          0.0131
		SemiMajorAxis   0.0401
		Eccentricity    0.0016
		Inclination     110.71
		AscendingNode   111.3
		ArgOfPericenter 217
		Epoch           2443746.4
		MeanAnomaly     0
	}
}

Star "MU Ori Bb" 
{
	ParentBody "MU Ori B"
	Class      "F5 V"
	Radius     910000
	AppMagn    6.27
	MassSol    1.4
	Orbit
	{
		Period          0.0131
		SemiMajorAxis   0.0401
		Eccentricity    0.0016
		Inclination     110.71
		AscendingNode   111.3
		ArgOfPericenter 37
		Epoch           2443746.4
		MeanAnomaly     0
	}
}

//PI4 Ori; spanish wiki

Star "PI4 Ori Aa/HIP 22549/HD 30836"
{
	ParentBody "PI4 Ori"
	Class      "B2 III"
	AppMagn    3.67
	MassSol    11
	Orbit
	{
		Period          0.0261
		SemiMajorAxis   0.119
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "PI4 Ori Ab"
{
	ParentBody "PI4 Ori"
	Class      "B2 IV"
	MassSol    10
	Orbit
	{
		Period          0.0261
		SemiMajorAxis   0.131
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//VV Ori; distance from celestia, other data from spanish wiki


Barycenter "VV Ori (AB)"
{
	ParentBody "VV Ori"
	Orbit
	{
		Period          0.326
		SemiMajorAxis   0.11
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "VV Ori A/HIP 26063/HD 36695"
{
	ParentBody "VV Ori (AB)"
	Radius     3640000
	Class      "B1 V"
	AppMagn    5.38
	MassSol    10.8
	Orbit
	{
		Period          0.0041
		SemiMajorAxis   0.0185
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "VV Ori B"
{
	ParentBody "VV Ori (AB)"
	Radius     1750000
	Class      "B7 V"
	MassSol    4.5
	Orbit
	{
		Period          0.0041
		SemiMajorAxis   0.0445
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "VV Ori C"
{
	ParentBody "VV Ori"
	Class      "A V"
	Orbit
	{
		Period          0.326
		SemiMajorAxis   0.89
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//CHI1 Ori;6thCVB;english and spanish wiki

Star "CHI1 Ori A/HIP 27913/HD 39587"
{
	ParentBody "CHI1 Ori"
	Class      "G0 V"
	Radius     730800
	AppMagn    4.41
	MassSol    1.03
	Orbit
	{
		Period          14.1172
		SemiMajorAxis   0.7754
		Eccentricity    0.452
		Inclination     95.937
		AscendingNode   126.36
		ArgOfPericenter 111.527
		Epoch           2451468.2
		MeanAnomaly     0
	}
}

Star "CHI1 Ori B"
{
	ParentBody "CHI1 Ori"
	Class      "M6 V"
	MassSol    0.15
	Orbit
	{
		Period          14.1172
		SemiMajorAxis   5.3246
		Eccentricity    0.452
		Inclination     95.937
		AscendingNode   126.36
		ArgOfPericenter 291.527
		Epoch           2451468.2
		MeanAnomaly     0
	}
}

//66 Ori;6thCVB,SIMBAD


Star "66 Ori A/HIP 28814 A/HD 41380 A"
{
	ParentBody "66 Ori"
	Class      "G4 III"
	AppMagn    5.64
	Orbit
	{
		Period          2.9892
		SemiMajorAxis   0.4569
		Eccentricity    0.246
		Inclination     105.6
		AscendingNode   20.6
		ArgOfPericenter 223.1
		Epoch           2451788
		MeanAnomaly     0
	}
}

Star "66 Ori B"
{
	ParentBody "66 Ori"
	AppMagn    11 		//unknown,SP companion
	Orbit
	{
		Period          2.9892
		SemiMajorAxis   3.7463
		Eccentricity    0.246
		Inclination     105.6
		AscendingNode   20.6
		ArgOfPericenter 43.1
		Epoch           2451788
		MeanAnomaly     0
	}
}



///////////////////LEO//////////////////////////////////

//Subra; 6thCVB, spanish wiki

Star "Subra A/HIP 47508/HD 83808"
{
	ParentBody "Subra"
	Class      "F6 III"
	Radius     3828000
	AppMagn    3.52
	MassSol    2.1
	Orbit
	{
		Period          0.0397
		SemiMajorAxis   0.0865
		Inclination     57.6
		AscendingNode   191.4
		ArgOfPericenter 0
		Epoch           2450629.831793
		MeanAnomaly     0
	}
}

Star "Subra B"
{
	ParentBody "Subra"
	Class      "A5 V"
	Radius     1809600
	MassSol    1.85
	Orbit
	{
		Period          0.0397
		SemiMajorAxis   0.0982
		Inclination     57.6
		AscendingNode   191.4
		ArgOfPericenter 180
		Epoch           2450629.831793
		MeanAnomaly     0
	}
}

//RHO Leo;spanish, english wiki

Star "RHO Leo A/HIP 51624/HD 91316"
{
	ParentBody "RHO Leo"
	Class      "B1 Ib"
	Radius     16008000
	AppMagn    4.4
	MassSol    23
	Orbit
	{
		Period          362.94808171
		SemiMajorAxis   91.1043
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "RHO Leo B"
{
	ParentBody "RHO Leo"
	Class      "O V" //unknown, related with     AppMagn
	AppMagn    4.8 //it could be also out the main sequence
	Orbit
	{
		Period          362.94808171
		SemiMajorAxis   91.1043
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//ETA Leo;spanish wiki

Star "ETA Leo A/HIP 49583/HD 87737"
{
	ParentBody "ETA Leo"
	Class      "A0 Ib"
	Radius     30624000
	AppMagn    3.52
	MassSol    7
	Orbit
	{
		Period          120
		SemiMajorAxis   15.5556
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "ETA Leo B"
{
	ParentBody "ETA Leo"
	Class      "A V" //unknown, related with the remnant system mass
	Orbit
	{
		Period          120
		SemiMajorAxis   54.4444
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//CHI Leo;spanish wiki

Star "CHI Leo A/HIP 54182/HD 96097"
{
	ParentBody "CHI Leo"
	Class      "F2 IV"
	Radius     1385040
	AppMagn    4.63
	MassSol    1.5
	Orbit
	{
		Period          615.23214755
		SemiMajorAxis   32.5525
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "CHI Leo B/NVC 5079"
{
	ParentBody "CHI Leo"
	Class      "K V" //unknown related with appmag
	AppMagn    11
	Orbit
	{
		Period          615.23214755
		SemiMajorAxis   62.6009
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//KAP Leo;

Star "Al Minliar al Asad A/KAP Leo A/HIP 46146/HD 81146"
{
	ParentBody "Al Minliar al Asad"
	Class      "K2 III"
	Radius     11832000
	AppMagn    4.46
	MassSol    2
	Orbit
	{
		Period          1040.14545343
		SemiMajorAxis   49.3252
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Al Minliar al Asad B/KAP Leo B"
{
	ParentBody "Al Minliar al Asad"
	Class      "G V" //related with absmag
	AppMagn    9.7
	MassSol    1
	Orbit
	{
		Period          1040.14545343
		SemiMajorAxis   98.6503
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//Regulus;WD present

//WD PRESENT

//IOT Leo;6thCVB

Star "IOT Leo A/HIP 55642/HD 99028"
{
	ParentBody "IOT Leo"
	Class      "F2 IV"
	AppMagn    4.06
	MassSol    1.6
	Orbit
	{
		Period          186
		SemiMajorAxis   17.78
		Eccentricity    0.53
		Inclination     128
		AscendingNode   235
		ArgOfPericenter 325
		Epoch           2432844.132821
		MeanAnomaly     0
	}
}

Star "IOT Leo B"
{
	ParentBody "IOT Leo"
	Class      "G3 V"
	AppMagn    6.71
	MassSol    1
	Orbit
	{
		Period          186
		SemiMajorAxis   28.448
		Eccentricity    0.53
		Inclination     128
		AscendingNode   235
		ArgOfPericenter 145
		Epoch           2432844.132821
		MeanAnomaly     0
	}
}

//39 Leo;

Star "39 Leo A/HIP 50384/HD 89125"
{
	ParentBody "39 Leo"
	Class      "F6 V"
	Radius     689040
	AppMagn    5.8
	MassSol    0.98
	Orbit
	{
		Period          2119.08897616
		SemiMajorAxis   34.6596
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "39 Leo B"
{
	ParentBody "39 Leo"
	Class      "M1 V"
	Radius     334080
	AppMagn    11.4
	MassSol    0.24
	Orbit
	{
		Period          2119.08897616
		SemiMajorAxis   141.5269
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//49 Leo;spanish wiki

Barycenter "49 Leo A"
{
	ParentBody "49 Leo"
	Orbit
	{
		Period          2330
		SemiMajorAxis   63.1452
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "49 Leo Aa/HIP 51802/HD 91636"
{
	ParentBody "49 Leo A"
	Class      "A2 V"
	Radius     2881440
	AppMagn    5.67
	MassSol    3.73
	Orbit
	{
		Period          0.00669863
		SemiMajorAxis   0.0242
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "49 Leo Ab"
{
	ParentBody "49 Leo A"
	Class      "A V" //unknown,related with Mass
	Radius     1691280
	MassSol    2.24
	Orbit
	{
		Period          0.00669863
		SemiMajorAxis   0.0403
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "49 Leo B"
{
	ParentBody "49 Leo"
	Class      "F V" //unknown, related with Mass
	AppMagn    8.1
	MassSol    1.69
	Orbit
	{
		Period          2330
		SemiMajorAxis   223.0634
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//54 Leo;sp wiki

Star "54 Leo A/HIP 53417"
{
	ParentBody "54 Leo"
	Class      "A1 V"
	Radius     1322400
	AppMagn    4.5
	MassSol    2.3
	Orbit
	{
		Period          7293.42163525
		SemiMajorAxis   303.381
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "54 Leo B"
{
	ParentBody "54 Leo"
	Class      "A2 V"
	Radius     835200
	AppMagn    6.3
	MassSol    2.2
	Orbit
	{
		Period          7293.42163525
		SemiMajorAxis   317.1711
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//88 Leo;6thCVB, spanish wiki

Star "88 Leo A/HIP 56242/HD 100180"
{
	ParentBody "88 Leo"
	Class      "G0 V"
	Radius     751680
	AppMagn    6.33
	MassSol    1.01
	Orbit
	{
		Period          3453
		SemiMajorAxis   159.3109
		Eccentricity    0.2
		Inclination     58
		AscendingNode   138
		ArgOfPericenter 100
		Epoch           2169577.555939
		MeanAnomaly     0
	}
}

Star "88 Leo B/LTT 13146"
{
	ParentBody "88 Leo"
	Class      "K5 V" //SIMBAD
	AppMagn    9.14
	Orbit
	{
		Period          3453
		SemiMajorAxis   214.5387
		Eccentricity    0.2
		Inclination     58
		AscendingNode   138
		ArgOfPericenter 280
		Epoch           2169577.555939
		MeanAnomaly     0
	}
}

//AP Leo;spanish wiki

Star "AP Leo Aa/HIP 54188"
{
	ParentBody "AP Leo"
	Class      "F9 V"
	Radius     1037040
	AppMagn    9.32
	MassSol    1.47
	Orbit
	{
		Period          0.00117918
		SemiMajorAxis   0.0032
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "AP Leo Ab"
{
	ParentBody "AP Leo"
	Class      "G V"
	Radius     605520
	MassSol    0.44
	Orbit
	{
		Period          0.00117918
		SemiMajorAxis   0.0107
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//WD STARS PRESENT IN CSS 41177, MOVED TO WD CATALOG

//FH Leo;spanish wiki

Star "FH Leo A/HD 96273/HIP 54268"
{
	ParentBody "FH Leo"
	Class      "F8 V"
	AppMagn    8.7
	Orbit
	{
		Period          20678.31770265
		SemiMajorAxis   447.8429
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "FH Leo B/BD+07 2411 B"
{
	ParentBody "FH Leo"
	Class      "G3 V"
	AppMagn    10.6
	Orbit
	{
		Period          20678.31770265
		SemiMajorAxis   528.4546
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//GZ Leo;spanish wiki

Star "GZ Leo A/HIP 53923/HD 95559"
{
	ParentBody "GZ Leo"
	Class      "K0 V"
	Radius     570720
	AppMagn    8.83
	MassSol    0.78
	Orbit
	{
		Period          0.00418265
		SemiMajorAxis   0.0152
		Eccentricity    0.0073
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "GZ Leo B"
{
	ParentBody "GZ Leo"
	Class      "K0 V"
	Radius     549840
	MassSol    0.79
	Orbit
	{
		Period          0.00418265
		SemiMajorAxis   0.015
		Eccentricity    0.0073
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//UZ Leo;spanish wiki
//contacting binary

Star "UZ Leo A/HIP 52249"
{
	ParentBody "UZ Leo"
	Class      "F2 V"
	Radius     1433760
	AppMagn    9.58
	MassSol    2.07
	Orbit
	{
		Period          0.00169329
		SemiMajorAxis   0.0042
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "UZ Leo B"
{
	ParentBody "UZ Leo"
	Class      "F V"
	Radius     835200
	MassSol    0.63
	Orbit
	{
		Period          0.00169329
		SemiMajorAxis   0.0138
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//XY Leo;6thCVB, spanish wiki
//XY Leo AB; contacting binary
//XY Leo CD; unknown Mass distribution and separation

Barycenter "XY Leo (AB)"
{
	ParentBody "XY Leo"
	Orbit
	{
		Period          19.59
		SemiMajorAxis   3.1756
		Eccentricity    0.09
		Inclination     65.8
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Barycenter "XY Leo (CD)"
{
	ParentBody "XY Leo"
	Orbit
	{
		Period          19.59
		SemiMajorAxis   3.9533
		Eccentricity    0.09
		Inclination     65.8
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "XY Leo A/HIP 49136"
{
	ParentBody "XY Leo (AB)"
	Class      "K0 V"
	Radius     459360
	AppMagn    9.67
	MassSol    0.46
	Orbit
	{
		Period          0.0008
		SemiMajorAxis   0.0056
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "XY Leo B"
{
	ParentBody "XY Leo (AB)"
	Class      "K0 V"
	Radius     577680
	MassSol    0.76
	Orbit
	{
		Period          0.0008
		SemiMajorAxis   0.0034
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "XY Leo C"
{
	ParentBody "XY Leo (CD)"
	Class      "M V"
	MassSol    0.49
	Orbit
	{
		Period          0.0319
		SemiMajorAxis   0.05
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "XY Leo D"
{
	ParentBody "XY Leo (CD)"
	Class      "M V"
	MassSol    0.49
	Orbit
	{
		Period          0.0319
		SemiMajorAxis   0.05
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//Y Leo;spanish wiki
//Close binary

Star "Y Leo A/HIP 47178"
{
	ParentBody "Y Leo"
	Class      "A3 V"
	Radius     1322400
	AppMagn    10.07
	MassSol    1.9
	Orbit
	{
		Period          0.00461945
		SemiMajorAxis   0.0085
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Y Leo B"
{
	ParentBody "Y Leo"
	Class      "K6 IV"
	Radius     1531200
	MassSol    0.56
	Orbit
	{
		Period          0.00461945
		SemiMajorAxis   0.0289
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//////////////////////////END OF LEO//////////////////////////////////

////////////////////////////ACRUX/////////////////////////////////////


Barycenter "Acrux A"
{
	ParentBody "Acrux"
	Orbit
	{
		Period          3.5616
		SemiMajorAxis   140.5405
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Acrux Aa/ALF1 Cru Aa/HIP 60718/HD 108248"
{
	ParentBody "Acrux A"
	Class      "B0 IV"
	AppMagn    0.77
	MassSol    14
	Orbit
	{
		Period          0.207
		SemiMajorAxis   0.4167
		Eccentricity    0.5
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "ALF1 Cru Ab"
{
	ParentBody "Acrux A"
	Class      "B V" //unknown, related with mass
	MassSol    10
	Orbit
	{
		Period          0.207
		SemiMajorAxis   0.5833
		Eccentricity    0.5
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "ALF2 Cru"
{
	ParentBody "Acrux"
	Class      "B1 V"
	MassSol    13
	Orbit
	{
		Period          3.5616
		SemiMajorAxis   259.4595
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}



//CD Cru; spanish wiki

Star "CD Cru A/HIP 62115/HD 311884"
{
	ParentBody "CD Cru"
	Class      "O5 V"
	Radius     8400000
	AppMagn    10.89
	MassSol    57
	Orbit
	{
		Period          0.0171
		SemiMajorAxis   0.1463
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "WR 47/CD Cru B"
{
	ParentBody "CD Cru"
	Class      "WN6"
	Radius     3500000
	MassSol    48
	Orbit
	{
		Period          0.0171
		SemiMajorAxis   0.1737
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//Mu1 Cru; spanish wiki, prof. jim kaler

Star "MU1 Cru/HIP 63003/HD 112092"
{
	ParentBody "MU Cru"
	Class      "B2 IV"
	AppMagn    4.03
	MassSol    3.1
	Orbit
	{
		Period          104953.6003
		SemiMajorAxis   1766.135
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "MU2 Cru"
{
	ParentBody "MU Cru"
	Class      "B5 V"
	AppMagn    5.08
	MassSol    2.5
	Orbit
	{
		Period          104953.6003
		SemiMajorAxis   2190.0075
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//TET1 Cru; spanish wiki, 9th catalog of spect binaries

Star "TET1 Cru Aa/HIP 58758/HD 104671"
{
	ParentBody "TET1 Cru"
	Class      "A V"
	AppMagn    4.32
	Orbit
	{
		Period          0.0671
		SemiMajorAxis   0.129
		Eccentricity    0.61
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "TET1 Cru Ab"
{
	ParentBody "TET1 Cru"
	Class      "A V"
	Orbit
	{
		Period          0.0671
		SemiMajorAxis   0.129
		Eccentricity    0.61
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

/////////////////////CANIS MAJOR///////////////////////////////////

Star	"Sirius A/Alhabor A/ALF CMa A/9 CMa A/Gliese 244 A"
{
	ParentBody  "Sirius"
	Class       "A1V"
	AppMagn     -1.43
	Age         0.23

	Orbit
	{
		Period          50.09
		SemiMajorAxis   6.73   // mass ratio 1.99:1.03
		Eccentricity    0.5923
		Inclination     97.51
		AscendingNode   161.33
		ArgOfPericenter 4.56
		MeanAnomaly     40.89
	}
}

Star	"Sirius B/Alhabor B/ALF CMa B/9 CMa B/Gliese 244 B"
{
	ParentBody  "Sirius"
	Class       "DA2"
	AppMagn     8.44
	Age         0.23
	NoAccretionDisk true

	Orbit
	{
		Period          50.09
		SemiMajorAxis   13.00  // mass ratio 1.99:1.03
		Eccentricity    0.592
		Inclination     97.51
		AscendingNode   161.33
		ArgOfPericenter 184.56
		MeanAnomaly     40.89
	}
}

//27 CMa

Barycenter "27 CMa A"
{
	ParentBody "27 CMa"
	Orbit
	{
		Period          0.0849
		SemiMajorAxis   16.1354
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "27 CMa Aa/HIP 34981/HD 56014"
{
	ParentBody "27 CMa A"
	Class      "B3 III"
	AppMagn    4.9
	Orbit
	{
		Period          0.00071781
		SemiMajorAxis   0.05 //unknown fictional
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "27 CMa Ab" //spectroscopic companion with unknown separation
{
	ParentBody "27 CMa A"
	AppMagn    10 //unknown
	Orbit
	{
		Period          0.00071781
		SemiMajorAxis   0.05 //unknown fictional
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "27 CMa B/EW CMa"
{
	ParentBody "27 CMa"
	Class      "B III"  //unknown, eruptive gamma cassiopeiae 
	AppMagn    5.4
	Orbit
	{
		Period          0.0849
		SemiMajorAxis   32.2709
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//Adhara; spanish wiki

Star "Adhara A/EPS CMa A/HIP 33579/HD 52089"
{
	ParentBody "Adhara"
	Class      "B2 Ia"
	Radius     7280000
	AppMagn    1.51
	MassSol    11
	Orbit
	{
		Period          7500
		SemiMajorAxis   111.4945
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Adhara B/EPS CMa B"
{
	ParentBody "Adhara"
	Class      "A V"
	AppMagn    8.5 //between 9 and 8
	Orbit
	{
		Period          7500
		SemiMajorAxis   757.0615
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//FZ CMa; spanish wiki, 3rd comp unknown     Class      and separation

Star "FZ CMa A/HIP 33953/HD 52942"
{
	ParentBody "FZ CMa"
	Class      "B2 VI"
	AppMagn    8.14
	MassSol    5
	Orbit
	{
		Period          0.0035
		SemiMajorAxis   0.0241
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "FZ CMa B"
{
	ParentBody "FZ CMa"
	Class      "B2 VI"
	MassSol    4.8
	Orbit
	{
		Period          0.0035
		SemiMajorAxis   0.0251
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


//R CMa; spanish wiki

Barycenter "R CMa (AB)"
{
	ParentBody "R CMa"
	Orbit
	{
		Period          0.2548
		SemiMajorAxis   5.1512
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "R CMa A/HIP 35487/HD 57167"
{
	ParentBody "R CMa (AB)"
	Class      "F0 V"
	Radius     1050000
	AppMagn    5.7
	MassSol    1.07
	Orbit
	{
		Period          0.00311205
		SemiMajorAxis   0.0036
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "R CMa B"
{
	ParentBody "R CMa (AB)"
	Class      "K1 IV"
	Radius     105000
	AppMagn    6.34
	MassSol    0.17
	Orbit
	{
		Period          0.00311205
		SemiMajorAxis   0.0228
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "R CMa C"
{
	ParentBody "R CMa"
	Class      "M4 V"
	MassSol    0.34
	Orbit
	{
		Period          0.2548
		SemiMajorAxis   18.7867
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//SW CMa; spanish wiki

Star "SW CMa A/HIP 34431/HD 54520"
{
	ParentBody "SW CMa"
	Class      "A3 IV"
	Radius     2100000
	MassSol    2.22
	Orbit
	{
		Period          0.0276
		SemiMajorAxis   0.0716
		Eccentricity    0.3179
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "SW CMa B"
{
	ParentBody "SW CMa"
	Class      "A8 IV"
	Radius     1750000
	MassSol    2.03
	Orbit
	{
		Period          0.0276
		SemiMajorAxis   0.0784
		Eccentricity    0.3179
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//UW CMa;eclipsing binary; wikipedia

Star "UW CMa A/HIP 35412/HD 57060"
{
	ParentBody "UW CMa"
	Class      "O7 Ia"
	Radius     9100000
	AppMagn    4.84
	MassSol    16
	Orbit
	{
		Period          0.012
		SemiMajorAxis   0.0933
		AscendingNode   109.66825 //unknown;AN aligned with RA for true eclipses
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "UW CMa B"
{
	ParentBody "UW CMa"
	Class      "O9 Ib"
	Radius     7000000
	AppMagn    5.33
	MassSol    19
	Orbit
	{
		Period          0.012
		SemiMajorAxis   0.0786
		AscendingNode   109.66825
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//Tau CMa; english wiki
//A B C D stars are around 20 Ms each

Barycenter "TAU Cma (ABC)"
{
	ParentBody "TAU Cma"
	Orbit
	{
		Period          165859.4377
		SemiMajorAxis   3250
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Barycenter "TAU Cma (AB)"
{
	ParentBody "TAU Cma (ABC)"
	Orbit
	{
		Period          430.28
		SemiMajorAxis   148.6667
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}



Star "TAU Cma A/HIP 35415/HD 57061"
{
	ParentBody "TAU Cma (AB)"
	Class      "O9 Ib"
	Radius     13780800
	AppMagn    4.37
	MassSol    20
	Orbit
	{
		Period          0.005
		SemiMajorAxis   0.05
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "TAU Cma B"
{
	ParentBody "TAU Cma (AB)"
	Class      "O V"
	MassSol    20
	Orbit
	{
		Period          0.005
		SemiMajorAxis   0.05
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "TAU Cma C"
{
	ParentBody "TAU Cma (ABC)"
	Class      "O V"
 
	AppMagn    5.3
	MassSol    20
	Orbit
	{
		Period          430.28
		SemiMajorAxis   74.3333
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "TAU Cma D"
{
	ParentBody "TAU Cma"
	Class      "O V"
	AppMagn    10
	MassSol    20
	Orbit
	{
		Period          165859.4377
		SemiMajorAxis   9750
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}


//Furud

Star "Furud A/ZET CMa A/HIP 30122/HD 44402"
{
	ParentBody "Furud"
	Radius     2730000
	Class      "B2 V"
	AppMagn    3.025
	MassSol    7.7
	Orbit
	{
		Period          1.8493
		SemiMajorAxis   1.8766
		Eccentricity    0.57
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "ZET CMa B" //spectroscopic companion
{
	ParentBody "Furud"
	Class      "B V" 
	Orbit
	{
		Period          1.8493
		SemiMajorAxis   1.8766 //if both stars have the same Mass
		Eccentricity    0.57
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

/////////////////////CANIS MINOR///////////////////////////////////

Star	"Procyon A/Elgomaisa A/ALF CMi A/10 CMi A/Gliese 280 A"
{
	ParentBody  "Procyon"
	Class       "F5IV"
	AppMagn     0.37
	MassSol     1.5
	Radius      1294560
	Temperature 6600
	Age         1.7

	Orbit
	{
		Period          40.82
		SemiMajorAxis   4.13
		Eccentricity    0.407
		Inclination     42.9
		AscendingNode   27.8
		ArgOfPericenter 88.4
		MeanAnomaly     282.5
	}
}

Star	"Procyon B/Elgomaisa B/ALF CMi B/10 CMi B/Gliese 280 B"
{
	ParentBody  "Procyon"
	Class       "DA1"
	AppMagn     10.75
	MassSol     0.6
	Radius      13920
	Temperature 9700
	NoAccretionDisk true

	Orbit
	{
		Period          40.82
		SemiMajorAxis   10.80
		Eccentricity    0.407
		Inclination     42.9
		AscendingNode   27.8
		ArgOfPericenter 268.4
		MeanAnomaly     282.5
	}
}

//ETA CMi;eng and sp wiki

Star "ETA CMi A/HIP 36284 A/HD 58972 A"
{
	ParentBody "ETA CMi"
	Class      "F0 III"
	Radius     4200000
	AppMagn    5.22
	MassSol    2.5
	Orbit
	{
		Period          5000
		SemiMajorAxis   112.619
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "ETA CMi B"
{
	ParentBody "ETA CMi"
	Class      "K1 V"
	AppMagn    11.1
	MassSol    0.86
	Orbit
	{
		Period          5000
		SemiMajorAxis   327.381
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//GAM CMi;Mass data from italian wiki, other data from wiki eng

Star "GAM CMi A/HIP 36265/HD 58972"
{
	ParentBody "GAM CMi"
	Radius     17500000
	Class      "K3 III"
	AppMagn    4.34
	MassSol    3
	Orbit
	{
		Period          1.0658
		SemiMajorAxis   0.884
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "GAM CMi B" 
{
	ParentBody "GAM CMi" //unknown, SP companion
	MassSol    2.7
	Orbit
	{
		Period          1.0658
		SemiMajorAxis   0.9822
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

/////////////////////CANCER/////////////////////////////////////////

//24 CNC; 24 cnc bc is binary, but with unknown data of both components; 6thCVB

Star "24 Cnc A/HIP 41389/HD 71153"
{
	ParentBody "24 Cnc"
	Class      "F0 III"
	AppMagn    8.5
	Orbit
	{
		Period          21.78
		SemiMajorAxis   5.9367
		Eccentricity    0.079
		Inclination     19.1
		AscendingNode   153.6
		ArgOfPericenter 51
		Epoch           2450967.450724
		MeanAnomaly     0
	}
}

Star "24 Cnc BC"
{
	ParentBody "24 Cnc"
	Class      "F7 V"
	AppMagn    8.5
	Orbit
	{
		Period          21.78
		SemiMajorAxis   5.9367
		Eccentricity    0.079
		Inclination     19.1
		AscendingNode   153.6
		ArgOfPericenter 231
		Epoch           2450967.450724
		MeanAnomaly     0
	}
}

//75 Cnc; 6thCVB, spanish wiki

Star "75 Cnc A/HIP 44892/HD 78418"
{
	ParentBody "75 Cnc"
	Class      "G5 IV"
	AppMagn    5.98
	MassSol    1.05  //between 0.9 and 1.2 Ms
	Orbit
	{
		Period          0.0532
		SemiMajorAxis   0.0726
		Eccentricity    0.19494
		Inclination     146.88
		AscendingNode   171.892
		ArgOfPericenter 283.389
		Epoch           2453895.4025
		MeanAnomaly     0
	}
}

Star "75 Cnc B"
{
	ParentBody "75 Cnc"
	Class      "K V" //unknown, just related with its Mass 
	MassSol    0.7
	Orbit
	{
		Period          0.0532
		SemiMajorAxis   0.109
		Eccentricity    0.19494
		Inclination     146.88
		AscendingNode   171.892
		ArgOfPericenter 103.389
		Epoch           2453895.4025
		MeanAnomaly     0
	}
}

//81 cnc;brown dwarf companion

//Altarf;with exoplanet

//HR 3617; Spanish wiki

Star "HR 3617 A/HIP 44758/HD 78175"
{
	ParentBody "HR 3617"
	Class      "F4 V"
	AppMagn    7
	MassSol    1.37
	Orbit
	{
		Period          5639.2092
		SemiMajorAxis   216.4855
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "HR 3617 B"
{
	ParentBody "HR 3617"
	Class      "F5 V"
	AppMagn    7.4
	MassSol    1.32
	Orbit
	{
		Period          5639.2092
		SemiMajorAxis   224.6858
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//Accubens; wikipedia


Barycenter "Acubens (AB)"
{
	ParentBody "Acubens"
	Orbit
	{
		Period          7166.54
		SemiMajorAxis   34.0971
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Acubens Aa/ALF Cnc Aa/HIP 44066/HD 76756"
{
	ParentBody "Acubens (AB)"
	Radius     700000
	Class      "A5 V"
	AppMagn    4.2
	MassSol    2
	Orbit
	{
		Period          6.14
		SemiMajorAxis   2.6654
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "ALF Cnc Ab" //suspected spectroscopic companion
{
	ParentBody "Acubens (AB)"
	AppMagn    9 //unknown
	Orbit
	{
		Period          6.14
		SemiMajorAxis   2.6654
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "ALF Cnc B"
{
	ParentBody "Acubens"
	Class      "M V"
	Orbit
	{
		Period          7166.54
		SemiMajorAxis   568.2856
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//KAP Cnc; pretty good description in english wikipedia

Star "KAP Cnc A/HIP 44798/HD 78316"
{
	ParentBody "KAP Cnc"
	Class      "B8 III"
	Radius     3500000
	AppMagn    5.23
	MassSol    4.5
	Orbit
	{
		Period          0.0175
		SemiMajorAxis   0.0403
		Eccentricity    0.14
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "KAP Cnc B" //spectroscopic companion
{
	ParentBody "KAP Cnc"
	Class      "A V" //related spectra with Teff of 8500K (around A4) it could be also in the main sequence
	Radius     1680000
	AppMagn    5.23
	MassSol    2.1
	Orbit
	{
		Period          0.0175
		SemiMajorAxis   0.0863
		Eccentricity    0.14
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//Tegmine;pretty good description in 6th CVB,english and spanish wiki

Barycenter "ZET1 CnC"
{
	ParentBody "Tegmine"
	Orbit
	{
		Period          1115
		SemiMajorAxis   88.1249
		Eccentricity    0.24
		Inclination     146
		AscendingNode   74.2
		ArgOfPericenter 345.5
		Epoch           2440587.267435
		MeanAnomaly     0
	}
}

Barycenter "ZET2 CnC"
{
	ParentBody "Tegmine"
	Orbit
	{
		Period          1115
		SemiMajorAxis   108.6191
		Eccentricity    0.24
		Inclination     146
		AscendingNode   74.2
		ArgOfPericenter 165.5
		Epoch           2440587.267435
		MeanAnomaly     0
	}
}


Star "Tegmine A/ZET Cnc A/HIP 40167/HD 68257"
{
	ParentBody "ZET1 CnC"
	Radius     2450000
	Class      "F7 V"
	AppMagn    5.3
	MassSol    1.4
	Orbit
	{
		Period          59.582
		SemiMajorAxis   10.3556
		Eccentricity    0.3186
		Inclination     173.94
		AscendingNode   157.6
		ArgOfPericenter 330.2
		Epoch           2447559.010525
		MeanAnomaly     0
	}
}

Star "ZET Cnc B"
{
	ParentBody "ZET1 CnC"
	Radius     1260000
	Class      "F9 V"
	AppMagn    6.25
	MassSol    1.25
	Orbit
	{
		Period          59.582
		SemiMajorAxis   11.5982
		Eccentricity    0.3186
		Inclination     173.94
		AscendingNode   157.6
		ArgOfPericenter 150.2
		Epoch           2447559.010525
		MeanAnomaly     0
	}
}


Star "ZET2 Cnc Ca/HD 68256"
{
	ParentBody "ZET2 CnC"
	Class      "G0 V"
	AppMagn    6.3
	MassSol    1.25
	Orbit
	{
		Period          17.32
		SemiMajorAxis   1.9413
		Eccentricity    0.08
		Inclination     142
		AscendingNode   77
		ArgOfPericenter 193
		Epoch           2445722.57275
		MeanAnomaly     0
	}
}

Star "ZET2 Cnc Cb" //may be 2 red dwarfs unresolved
{
	ParentBody "ZET2 CnC"
	Class      "K V" //unknown related with mass
	MassSol    0.9
	Orbit
	{
		Period          17.32
		SemiMajorAxis   2.6962
		Eccentricity    0.08
		Inclination     142
		AscendingNode   77
		ArgOfPericenter 13
		Epoch           2445722.57275
		MeanAnomaly     0
	}
}

//TX Cnc; spanish wiki; eclipsing contact binary

Star "TX Cnc A"
{
	ParentBody "TX Cnc"
	Class      "F8 V"
	Radius     889000
	AppMagn    10
	MassSol    1.32
	Orbit
	{
		Period          0.001
		SemiMajorAxis   0.0041
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "TX Cnc B"
{
	ParentBody "TX Cnc"
	Radius     623000
	Class      "K V" //related with Mass
	MassSol    0.61
	Orbit
	{
		Period          0.001
		SemiMajorAxis   0.0088
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//WY Cnc; spanish wiki, eclipsing binary

Star "WY Cnc A/HIP 44349"
{
	ParentBody "WY Cnc"
	Class      "G5 V"
	Radius     700000
	AppMagn    9.47
	MassSol    0.85
	Orbit
	{
		Period          0.0023
		SemiMajorAxis   0.0071
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "WY Cnc B"
{
	ParentBody "WY Cnc"
	Radius     420000
	Class      "K4 V"
	Radius     420000
	MassSol    0.5
	Orbit
	{
		Period          0.0023
		SemiMajorAxis   0.0121
		AscendingNode   0 
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

/////////////////////CAPRICORNUS///////////////////////////////////

//Dabih; BET1 Cap AaAb Orbit from 6thCVB
//BET1 Cap Ac unknown Class

Barycenter "BET1 Cap/Dabih Maior"
{
	ParentBody "Dabih"
	Orbit
	{
		Period          700000
		SemiMajorAxis   7000
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Barycenter "BET2 Cap/Dabih Minor"
{
	ParentBody "Dabih"
	Orbit
	{
		Period          700000
		SemiMajorAxis   14000
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


Barycenter "BET1 Cap AbAc"
{
	ParentBody "BET1 Cap"
	Orbit
	{
		Period          0.0103
		SemiMajorAxis   4.9239
		Eccentricity    0.432
		Inclination     92.62
		AscendingNode   211
		ArgOfPericenter 245.58
		MeanAnomaly     0
		Epoch           2421225.778477
	}
}

Star "Dabih A/BET1 Cap Aa/HIP 193495/HD 100345" //suspected binary too
{
	ParentBody "BET1 Cap"
	Radius     24500000
	Class      "K0 III"
	AppMagn    3.1
	MassSol    4.9
	Orbit
	{
		Period          0.0103
		SemiMajorAxis   4.0196
		Eccentricity    0.432
		Inclination     92.62
		AscendingNode   211
		ArgOfPericenter 65.58
		MeanAnomaly     0
		Epoch           2421225.778477
	}
}

Star "BET1 Cap Ab"
{
	ParentBody "BET1 Cap AbAc"
	Class      "B8 V"
	AppMagn    4.9
	MassSol    3.76
	Orbit
	{
		Period          0.02383562
		SemiMajorAxis   0.006
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "BET1 Cap Ac"
{
	ParentBody "BET1 Cap AbAc"
	Class      "M V"
	Orbit
	{
		Period          0.02383562
		SemiMajorAxis   0.094
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}


Star "BET2 Cap B/HIP 100325/HD 193452"
{
	ParentBody "BET2 Cap"
	Class      "B9 III"
	AppMagn    6.09
	Orbit
	{
		Period          80.801
		SemiMajorAxis   9.6117
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "BET2 Cap C"
{
	ParentBody "BET2 Cap"
	Class      "F5 V"
	Orbit
	{
		Period          80.801
		SemiMajorAxis   20.3883
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//Deneb Algedi; english wiki


Star "Deneb Algedi A/DEL Cap A/HIP 107556/HD 207098"
{
	ParentBody "Deneb Algedi"
	Class      "A7 III"
	Radius     1337000
	AppMagn    2.81
	MassSol    2
	Orbit
	{
		Period          0.0028
		SemiMajorAxis   0.0105
		Inclination     72.5
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "DEL Cap B"
{
	ParentBody "Deneb Algedi"
	Class      "K V"   //unknown, related with Mass
	MassSol    0.9
	Orbit
	{
		Period          0.0028
		SemiMajorAxis   0.0233
		Inclination     72.5
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


//ZET Cap;WD present

//Ras Algethi; wikipedia
//ALF Her A has two more close companions with unknown spectral     Class

Barycenter "Ras Algethi B"
{
	ParentBody "Ras Algethi"
	Orbit
	{
		Period          9.863
		SemiMajorAxis   274.1963
		Inclination     155.8
		AscendingNode   119.6
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Ras Algethi A/ALF Her A/HIP 84345/HD 156014" //2 more companions, unknown Mass and     Class
{
	ParentBody "Ras Algethi"
	Radius     269352000
	Class      "M5 II"
	AppMagn    2.9137
	MassSol    2.15
	Orbit
	{
		Period          9.863
		SemiMajorAxis   274.1963
		Inclination     155.8
		AscendingNode   119.6
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "ALF Her Ba"
{
	ParentBody "Ras Algethi B"
	Class      "G5 III"
	AppMagn    5.4
	Orbit
	{
		Period          0.14246575
		SemiMajorAxis   0.2
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "ALF Her Bb"
{
	ParentBody "Ras Algethi B"
	Class      "F2 V"
	AppMagn    5.4
	Orbit
	{
		Period          0.14246575
		SemiMajorAxis   0.2
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//Kornephoros; 6thCVB, wikipedia

Star "Kornephoros A/BET Her A/HIP 80816/HD 148856"
{
	ParentBody "Kornephoros"
	Radius     11832000
	Class      "G7 III"
	AppMagn    2.79
	MassSol    2.9
	Orbit
	{
		Period          1.1249
		SemiMajorAxis   0.1111
		Eccentricity    0.5498
		Inclination     46.38
		AscendingNode   17.81
		ArgOfPericenter 24.61
		Epoch           2448393.0625
		MeanAnomaly     0
	}
}

Star "BET Her B"
{
	ParentBody "Kornephoros"
	Class      "G V"
	MassSol    0.9
	Orbit
	{
		Period          1.1249
		SemiMajorAxis   0.3579
		Eccentricity    0.5498
		Inclination     46.38
		AscendingNode   17.81
		ArgOfPericenter 204.61
		Epoch           2448393.0625
		MeanAnomaly     0
	}
}

//Sarin;spanish wiki, prof. jim kaler website

Star "Sarin A/DEL Her A/HIP 84379/HD 156164"
{
	ParentBody "Sarin"
	Class      "A3 V"
	Radius     1392000
	AppMagn    3.14
	MassSol    2
	Orbit
	{
		Period          0.9178
		SemiMajorAxis   0.6444
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "DEL Her B"
{
	ParentBody "Sarin"
	Class      "F0 V"
	Radius     1044000
	MassSol    1.6
	Orbit
	{
		Period          0.9178
		SemiMajorAxis   0.8056
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//EPS Her;spanish wiki, prof. jim kaler website, spectroscopic binary

Star "EPS Her A/HIP 83207/HD 153808"
{
	ParentBody "EPS Her"
	Class      "A0 V"
	Radius     1322400
	AppMagn    3.91
	MassSol    2.5
	Orbit
	{
		Period          0.0111
		SemiMajorAxis   0.0425
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "EPS Her B"
{
	ParentBody "EPS Her"
	Class      "A2 V"
	Radius     1252800
	MassSol    2.5
	Orbit
	{
		Period          0.0111
		SemiMajorAxis   0.0425
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//PHI Her;    Orbit from 6thCVB other data spanish wiki

Star "PHI Her A/HIP 79101/HD 145389"
{
	ParentBody "PHI Her"
	Class      "B8 V"
	Radius     2088000
	AppMagn    4.23
	MassSol    3.05
	Orbit
	{
		Period          1.5471
		SemiMajorAxis   0.694
		Eccentricity    0.522
		Inclination     12.1
		AscendingNode   9.1
		ArgOfPericenter 351.9
		Epoch           2450121.8
		MeanAnomaly     0
	}
}

Star "PHI Her B"
{
	ParentBody "PHI Her"
	Class      "A8 V"
	MassSol    1.61
	Orbit
	{
		Period          1.5471
		SemiMajorAxis   1.3147
		Eccentricity    0.522
		Inclination     12.1
		AscendingNode   9.1
		ArgOfPericenter 171.9
		Epoch           2450121.8
		MeanAnomaly     0
	}
}


//FN Her; eclipsing binary; spanish wiki

Star "FN Her A"
{
	ParentBody "FN Her"
	Class      "A9 V"
	AppMagn    11.08
	MassSol    1.99
	Orbit
	{
		Period          0.0074
		SemiMajorAxis   0.0254
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "FN Her B"
{
	ParentBody "FN Her"
	Class      "F V"
	MassSol    1.46
	Orbit
	{
		Period          0.0074
		SemiMajorAxis   0.0346
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//GAM Her;spanish wiki, spectroscopic binary

Star "GAM Her A/HIP 80170/HD 147547"
{
	ParentBody "GAM Her"
	Class      "A9 III"
	Radius     4176000
	AppMagn    3.74
	MassSol    2.6
	Orbit
	{
		Period          0.0326
		SemiMajorAxis   0.095
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "GAM Her B"
{
	ParentBody "GAM Her"
	AppMagn    7 //unknown, SP companion
	Orbit
	{
		Period          0.0326
		SemiMajorAxis   0.095
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//Gliese 623; 6thCVB;spanish wiki

Star "Gliese 623 A/HIP 80346"
{
	ParentBody "Gliese 623"
	Class      "M3 V"
	AppMagn    10.27
	MassSol    0.371
	Orbit
	{
		Period          3.7427
		SemiMajorAxis   0.4361
		Eccentricity    0.631
		Inclination     154
		AscendingNode   98.5
		ArgOfPericenter 248.68
		Epoch           2451313.3
		MeanAnomaly     0
	}
}

Star "Gliese 623 B"
{
	ParentBody "Gliese 623"
	Class      "M V"
	MassSol    0.11
	Orbit
	{
		Period          3.7427
		SemiMajorAxis   1.4709
		Eccentricity    0.631
		Inclination     154
		AscendingNode   98.5
		ArgOfPericenter 68.68
		Epoch           2451313.3
		MeanAnomaly     0
	}
}

//IOT Her; eng and spanish wiki
//2 more companions but unknown     Class

Star "IOT Her A/HIP 86414/HD 160762"
{
	ParentBody "IOT Her"
	Class      "B3 IV"
	Radius     3688800
	AppMagn    3.79
	MassSol    6.5
	Orbit
	{
		Period          0.3118
		SemiMajorAxis   0.3657
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "IOT Her B"
{
	ParentBody "IOT Her"
	Class      "B V" //10.24 MS total for the binary (3rd kepler law)
	Orbit
	{
		Period          0.3118
		SemiMajorAxis   0.6343
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//MU Her;AaAb and BC     Orbits from 6thCVB, other data from eng and sp wiki
//good star system

Barycenter "MU Her A"
{
	ParentBody "MU Her"
	Orbit
	{
		Period          3445
		SemiMajorAxis   95.8115
		Inclination     68      //inclination and node just aligned with AaAb
		AscendingNode   81.8
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Barycenter "MU Her BC"
{
	ParentBody "MU Her"
	Orbit
	{
		Period          3445
		SemiMajorAxis   204.1885
		Inclination     68
		AscendingNode   81.8
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


Star "MU Her Aa/HIP 86974/HD 161797/GJ 695 A"
{
	ParentBody "MU Her A"
	Class      "G5 IV"
	Radius     1224960
	AppMagn    3.42
	MassSol    1.1
	Orbit
	{
		Period          65
		SemiMajorAxis   0.3427
		Eccentricity    0.32
		Inclination     68
		AscendingNode   81.8
		ArgOfPericenter 92
		Epoch           2433647.665658
		MeanAnomaly     0
	}
}

Star "MU Her Ab"
{
	ParentBody "MU Her A"
	Class      "M V"
	AppMagn    12.7
	MassSol    0.2
	Orbit
	{
		Period          65
		SemiMajorAxis   1.8846
		Eccentricity    0.32
		Inclination     68
		AscendingNode   81.8
		ArgOfPericenter 272
		Epoch           2433647.665658
		MeanAnomaly     0
	}
}

Star "MU Her B/LHS 3325/GJ 695 B"
{
	ParentBody "MU Her BC"
	Class      "M3 V"
	Radius     334080
	AppMagn    10.2
	MassSol    0.31
	Orbit
	{
		Period          43.2
		SemiMajorAxis   5.6216
		Eccentricity    0.178
		Inclination     66.2
		AscendingNode   60.7
		ArgOfPericenter 174
		Epoch           2438907.15332
		MeanAnomaly     0
	}
}

Star "MU Her C/GJ 695 C"
{
	ParentBody "MU Her BC"
	Class      "M V"
	Radius     278400
	AppMagn    10.7
	MassSol    0.3
	Orbit
	{
		Period          43.2
		SemiMajorAxis   5.809
		Eccentricity    0.178
		Inclination     66.2
		AscendingNode   60.7
		ArgOfPericenter 354
		Epoch           2438907.15332
		MeanAnomaly     0
	}
}

//RHO Her;spanish wiki

Barycenter "RHO Her (AB)"
{
	ParentBody "RHO Her"
	Orbit
	{
		Period          694113.69
		SemiMajorAxis   1325.1534
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}


Star "RHO Her A/HIP 85112/HD 157779"
{
	ParentBody "RHO Her (AB)"
	Class      "B9 III"
	Radius     3340800
	AppMagn    4.56
	MassSol    3.2
	Orbit
	{
		Period          4518.28
		SemiMajorAxis   237.7049
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "RHO Her B"
{
	ParentBody "RHO Her (AB)"
	Class      "A0 V"
	Radius     2366400
	AppMagn    4.72
	MassSol    2.9
	Orbit
	{
		Period          4518.28
		SemiMajorAxis   262.2951
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "RHO Her C"
{
	ParentBody "RHO Her"
	Class      "K V"
	AppMagn    13
	MassSol    0.6
	Orbit
	{
		Period          694113.69
		SemiMajorAxis   13472.3926
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//UX Her;spanish wiki, suspected 3rd component
//semi detached close binary

Star "UX Her A/HIP 87643/HD 163175"
{
	ParentBody "UX Her"
	Class      "A0 V"
	Radius     1392000
	AppMagn    9.05
	MassSol    2.7
	Orbit
	{
		Period          0.0042
		SemiMajorAxis   0.0071
		Eccentricity    0.08
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "UX Her B"
{
	ParentBody "UX Her"
	Class      "A V" 
	Radius     1322400
	MassSol    0.6
	Orbit
	{
		Period          0.0042
		SemiMajorAxis   0.032
		Eccentricity    0.08
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//ZET Her;6thCVB, english wiki
//very good system

Star "ZET Her A/HIP 81693/HD 150680"
{
	ParentBody "ZET Her"
	Class      "F9 IV"
	Radius     1781760
	AppMagn    2.95
	MassSol    1.45
	Orbit
	{
		Period          34.45
		SemiMajorAxis   5.7587
		Eccentricity    0.46
		Inclination     131
		AscendingNode   50
		ArgOfPericenter 111
		Epoch           2439747.210377
		MeanAnomaly     0
	}
}

Star "ZET Her B"
{
	ParentBody "ZET Her"
	Class      "G7 V"
	Radius     636840
	AppMagn    5.4
	MassSol    0.98
	Orbit
	{
		Period          34.45
		SemiMajorAxis   8.5205
		Eccentricity    0.46
		Inclination     131
		AscendingNode   50
		ArgOfPericenter 291
		Epoch           2439747.210377
		MeanAnomaly     0
	}
}

//68 Her; spanish wiki

Barycenter "68 Her A"
{
	ParentBody "68 Her"
	Orbit
	{
		Period          10900
		SemiMajorAxis   185.7223
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "68 Her Aa/HIP 84573/HD 156633"
{
	ParentBody "68 Her A"
	Class      "B1 V"
	AppMagn    4.8
	MassSol    6.8
	Orbit
	{
		Period          0.00561644
		SemiMajorAxis   0.0204
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "68 Her Ab"
{
	ParentBody "68 Her A"
	Class      "B5 V"
	MassSol    2.8
	Orbit
	{
		Period          0.00561644
		SemiMajorAxis   0.0496
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "68 Her B"
{
	ParentBody "68 Her"
	Class      "A V"
	AppMagn    10.2
	MassSol    2.5
	Orbit
	{
		Period          10900
		SemiMajorAxis   713.1735
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//95 Her; jim kaler, spanish wiki

Star "95 Her A/HIP 88267/HD 164669"
{
	ParentBody "95 Her"
	Class      "A5 III"
	Radius     4732800
	AppMagn    4.96
	MassSol    2.8
	Orbit
	{
		Period          11154.216
		SemiMajorAxis   484.4172
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "95 Her B"
{
	ParentBody "95 Her"
	Class      "G8 III"
	Radius     13502400
	AppMagn    5.18
	MassSol    3.2
	Orbit
	{
		Period          11154.216
		SemiMajorAxis   423.865
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//105 Her;6thCVB;SIMBAD

Star "105 Her A/HD 168532 A/HIP 89773 A"
{
	ParentBody "105 Her"
	Class      "K4 Ia" 		//K4 Iab not recognized in SE 0.973
	AppMagn    3.74
	Orbit
	{
		Period          2.9892
		SemiMajorAxis   0.1731
		Eccentricity    0.246
		Inclination     105.6
		AscendingNode   20.6
		ArgOfPericenter 223.1
		Epoch           2451788
		MeanAnomaly     0
	}
}

Star "105 Her B"
{
	ParentBody "105 Her"
	AppMagn    12 			//Unknown, SP companion
	Orbit
	{
		Period          2.9892
		SemiMajorAxis   0.6925
		Eccentricity    0.246
		Inclination     105.6
		AscendingNode   20.6
		ArgOfPericenter 43.1
		Epoch           2451788
		MeanAnomaly     0
	}
}

//////////////////////////////////DRACO//////////////////////////////////

//Thuban;6thCVB, english wiki

Star "Thuban A/ALF Dra A/HIP 68756/HD 123299"
{
	ParentBody "Thuban"
	Class      "A0 III"
	Radius     2366400
	AppMagn    3.65
	MassSol    2.8
	Orbit
	{
		Period          0.1409
		SemiMajorAxis   0.0206
		Eccentricity    0.4
		Inclination     131.78
		AscendingNode   241.14
		ArgOfPericenter 23.2
		Epoch           2445117.375
		MeanAnomaly     0
	}
}

Star "ALF Dra B"
{
	ParentBody "Thuban"
	Class      "M V" //could be also a white dwarf
	Orbit
	{
		Period          0.1409
		SemiMajorAxis   0.1151
		Eccentricity    0.4
		Inclination     131.78
		AscendingNode   241.14
		ArgOfPericenter 203.2
		Epoch           2445117.375
		MeanAnomaly     0
	}
}


//Chi Dra; 6thCVB; spanish wiki


Star "CHI Dra A/HIP 89937/HD 170153"
{
	ParentBody "CHI Dra"
	Class      "F7 V"
	Radius     779520
	AppMagn    3.57
	MassSol    1.03
	Orbit
	{
		Period          0.7686
		SemiMajorAxis   0.4371
		Eccentricity    0.428
		Inclination     74.42
		AscendingNode   230.3
		ArgOfPericenter 119.3
		Epoch           2446004.68
		MeanAnomaly     0
	}
}

Star "CHI Dra B"
{
	ParentBody "CHI Dra"
	Class      "K0 V"
 
	AppMagn    5.7
	MassSol    0.74
	Orbit
	{
		Period          0.7686
		SemiMajorAxis   0.6084
		Eccentricity    0.428
		Inclination     74.42
		AscendingNode   230.3
		ArgOfPericenter 299.3
		Epoch           2446004.68
		MeanAnomaly     0
	}
}

//PHI Dra; AB     Orbit from 6thCVB, spanish wiki, prof. jim kaler website

Barycenter "PHI Dra A"
{
	ParentBody "PHI Dra"
	Orbit
	{
		Period          307.8
		SemiMajorAxis   26.6651
		Eccentricity    0.752
		Inclination     95.6
		AscendingNode   70.3
		ArgOfPericenter 275
		Epoch           2494131.773776
		MeanAnomaly     0
	}
}


Star "PHI Dra Aa/HIP 89908/HD 170000"
{
	ParentBody "PHI Dra A"
	Class      "A0 V"
	AppMagn    4.5
	MassSol    3.2
	Orbit
	{
		Period          0.0732
		SemiMajorAxis   0.1154
		Inclination     95.6 //RA and IN unknown just aligned
		AscendingNode   70.3
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "PHI Dra Ab"
{
	ParentBody "PHI Dra A"
	Class      "A V" 
	MassSol    2
	Orbit
	{
		Period          0.0732
		SemiMajorAxis   0.1846
		Inclination     95.6 //RA and IN unknown just aligned
		AscendingNode   70.3
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "PHI Dra B"
{
	ParentBody "PHI Dra"
	Class      "A4 V"
	AppMagn    5.9
	MassSol    2.2
	Orbit
	{
		Period          307.8
		SemiMajorAxis   63.0266
		Eccentricity    0.752
		Inclination     95.6
		AscendingNode   70.3
		ArgOfPericenter 95
		Epoch           2494131.773776
		MeanAnomaly     0
	}
}

//Arrakis;     Orbits from 6thCVB except C, spanish wiki

Barycenter "MU Dra AB"
{
	ParentBody "Arrakis"
	Orbit
	{
		Period          4000
		SemiMajorAxis   42.9058
		Inclination     142.2        //AN and IN unknown just aligned with AB     Orbit
		AscendingNode   282.85
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Barycenter "MU Dra B"
{
	ParentBody "MU Dra AB"
	Orbit
	{
		Period          812
		SemiMajorAxis   45.3497
		Eccentricity    0.5139
		Inclination     142.2
		AscendingNode   282.85
		ArgOfPericenter 13.31
		Epoch           2431890.850682
		MeanAnomaly     0
	}
}

Star "Arrakis A/MU Dra A/HIP 83608/HD 154905"
{
	ParentBody "MU Dra AB"
	Class      "F5 V"
	Radius     1044000
	AppMagn    5.66
	MassSol    1.2
	Orbit
	{
		Period          812
		SemiMajorAxis   75.5828
		Eccentricity    0.5139
		Inclination     142.2
		AscendingNode   282.85
		ArgOfPericenter 193.31
		Epoch           2431890.850682
		MeanAnomaly     0
	}
}

Star "MU Dra Ba"
{
	ParentBody "MU Dra B"
	Class      "F5 V"
	Radius     1044000
	AppMagn    5
	MassSol    1.1
	Orbit
	{
		Period          3.2
		SemiMajorAxis   0.3158
		Eccentricity    0.4
		Inclination     83.5
		AscendingNode   134.1
		ArgOfPericenter 272.6
		Epoch           2420645.043381
		MeanAnomaly     0
	}
}

Star "MU Dra Bb"
{
	ParentBody "MU Dra B"
	Class      "G4 V"
	MassSol    0.9
	Orbit
	{
		Period          3.2
		SemiMajorAxis   0.386
		Eccentricity    0.4
		Inclination     83.5
		AscendingNode   134.1
		ArgOfPericenter 92.6
		Epoch           2420645.043381
		MeanAnomaly     0
	}
}

Star "MU Dra C"
{
	ParentBody "Arrakis"
	Class      "M3 V"
	AppMagn    11.7
	Orbit
	{
		Period          4000
		SemiMajorAxis   343.2468
		Inclination     142.2      //AN and IN unknown just aligned with AB     Orbit
		AscendingNode   282.85
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


//Kuma;spanish wiki, prof. jim kaler website stars

Star "Kuma 1/NU1 Dra/24 Dra/HIP 85819/HD 159541"
{
	ParentBody "Kuma"
	Class      "A6 V"
	AppMagn    4.86
	MassSol    1.7
	Orbit
	{
		Period          44223.9909
		SemiMajorAxis   941.411
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Kuma 2/NU2 Dra/25 Dra/HIP 85829/HD 159560"
{
	ParentBody "Kuma"
	Class      "A4 V"
	AppMagn    4.89
	MassSol    1.7
	Orbit
	{
		Period          44223.9909
		SemiMajorAxis   941.411
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//TET Dra;spanish wiki, prof. jim kaler stars website

Star "TET Dra A/HIP 78527/HD 144284"
{
	ParentBody "TET Dra"
	Class      "F8 IV"
	Radius     1740000
	AppMagn    4.01
	MassSol    1.21
	Orbit
	{
		Period          0.0084
		SemiMajorAxis   0.0138
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "TET Dra B"
{
	ParentBody "TET Dra"
	Class      "M V"
	MassSol    0.46
	Orbit
	{
		Period          0.0084
		SemiMajorAxis   0.0362
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//OME Dra

//The Astrophysical Journal, 719:1293–1314, 2010 August 20
//HIGH-PRECISION     OrbitAL AND PHYSICAL PARAMETERS OF DOUBLE-LINED SPECTROSCOPIC
//BINARY STARS—HD78418, HD123999, HD160922, HD200077, AND HD210027
//Maciej Konacki, Matthew W. Muterspaugh, Shrinivas R. Kulkarni,
//and Krzysztof G. Hellminiak

Star "OME Dra A/HIP 86201/HD 160922"
{
	ParentBody "OME Dra"
	Class      "F4 V"
	AppMagn    4.8
	MassSol    1.46
	Orbit
	{
		Period          0.0145
		SemiMajorAxis   0.0367
		Eccentricity    0.0022
		Inclination     151.4
		AscendingNode   1.23
		ArgOfPericenter 314.8
		Epoch           2454348.583
		MeanAnomaly     0
	}
}

Star "OME Dra B"
{
	ParentBody "OME Dra"
	Class      "F V" //unknown related with Mass
	MassSol    1.18
	Orbit
	{
		Period          0.0145
		SemiMajorAxis   0.0454
		Eccentricity    0.0022
		Inclination     151.4
		AscendingNode   1.23
		ArgOfPericenter 134.8
		Epoch           2454348.583
		MeanAnomaly     0
	}
}


//UI Dra; Spanish wiki

Star "UI Dra A/GJ 577 A/HIP 73869/HD 134319"
{
	ParentBody "UI Dra"
	Class      "G5 V"
	Radius     633360
	AppMagn    8.41
	MassSol    0.95
	Orbit
	{
		Period          3600
		SemiMajorAxis   48.2017
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "UI Dra B/GJ 577 B"
{
	ParentBody "UI Dra"
	Class      "M4 V"
	Orbit
	{
		Period          3600
		SemiMajorAxis   190.7983
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//Z Dra; spanish wiki, contacting binary
//transfer of Mass from B to A

Star "Z Dra A/HIP 57348"
{
	ParentBody "Z Dra"
	Class      "A5 V"
	Radius     1085760
	AppMagn    4.14
	MassSol    1.4
	Orbit
	{
		Period          0.0037
		SemiMajorAxis   0.0062
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Z Dra B"
{
	ParentBody "Z Dra"
	Class      "K V" //unknown, related with temperature (4575 K)
	Radius     1085760
	MassSol    0.38
	Orbit
	{
		Period          0.0037
		SemiMajorAxis   0.0229
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//16-17 Dra, spanish wiki, prof. jim kaler
//problably 16 Dra has white dwarf companion

Barycenter "17 Dra (AB)"
{
	ParentBody "17 Dra"
	Orbit
	{
		Period          3800
		SemiMajorAxis   4058.8235
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "17 Dra A/HIP 81292/HD 150117"
{
	ParentBody "17 Dra (AB)"
	Class      "B9 V"
	Radius     2366400
	AppMagn    5.03
	MassSol    3.1
	Orbit
	{
		Period          3800
		SemiMajorAxis   185.4545
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "17 Dra B"
{
	ParentBody "17 Dra (AB)"
	Class      "A1 V"
	Radius     1670400
 
	MassSol    2.4
	Orbit
	{
		Period          3800
		SemiMajorAxis   239.5455
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "16 Dra/HIP 81290/HD 150100"
{
	ParentBody "17 Dra"
	Class      "B9 V"
	Radius     2227200
	AppMagn    5.53
	MassSol    3
	Orbit
	{
		Period          3800
		SemiMajorAxis   7441.1765
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//26 Dra

Barycenter "26 Dra (AB)"
{
	ParentBody "26 Dra"
	Orbit
	{
		Period          735830.21
		SemiMajorAxis   1478.8732
		Inclination     104  //Unknown RA and IN just aligned with AB
		AscendingNode   151
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "26 Dra A/GJ 684 A/HIP 86036/HD 160269"
{
	ParentBody "26 Dra (AB)"
	Class      "G0 V"
	Radius     765600
	AppMagn    5.28
	MassSol    1.08
	Orbit
	{
		Period          76.1
		SemiMajorAxis   8.9753
		Eccentricity    0.18
		Inclination     104
		AscendingNode   151
		ArgOfPericenter 307
		Epoch           2432186.696863
		MeanAnomaly     0
	}
}

Star "26 Dra B/GJ 684 B"
{
	ParentBody "26 Dra (AB)"
	Class      "K3 V"
	Radius     522000
	AppMagn    8.54
	MassSol    0.76
	Orbit
	{
		Period          76.1
		SemiMajorAxis   12.7544
		Eccentricity    0.18
		Inclination     104
		AscendingNode   151
		ArgOfPericenter 127
		Epoch           2432186.696863
		MeanAnomaly     0
	}
}

Star "26 Dra C/GJ 685/LHS 3306"
{
	ParentBody "26 Dra"
	Class      "M0 V"
	AppMagn    9.97
	MassSol    0.3
	Orbit
	{
		Period          735830.21
		SemiMajorAxis   9021.1268
		Inclination     104 //Unknown RA and IN just aligned with AB
		AscendingNode   151
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//AG Dra;WD STAR PRESENT

//AS Dra; spanish wiki

Barycenter "AS Dra (AB)"
{
	ParentBody "AS Dra"
	Orbit
	{
		Period          18.5781
		SemiMajorAxis   0.5744
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "AS Dra A/HIP 60331/HD 107760"
{
	ParentBody "AS Dra (AB)"
	Class      "G4 V"
	Radius     682080
	AppMagn    8
	MassSol    0.84
	Orbit
	{
		Period          0.01482877
		SemiMajorAxis   0.0294
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "AS Dra B"
{
	ParentBody "AS Dra (AB)"
	Class      "G9 V"
	Radius     577680
	MassSol    0.61
	Orbit
	{
		Period          0.01482877
		SemiMajorAxis   0.0406
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "AS Dra C"
{
	ParentBody "AS Dra"
	Class      "M V"
	MassSol    0.11
	Orbit
	{
		Period          18.5781
		SemiMajorAxis   7.5712
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//BY Dra;6thCVB for AB     Orbit, spanish wiki
//A B are Pre-main sequence stars

Barycenter "BY Dra (AB)"
{
	ParentBody "BY Dra"
	Orbit
	{
		Period          2880.73
		SemiMajorAxis   13.5545
		Inclination     154.41 //AN and IN unknown just aligned with ab     Orbit
		AscendingNode   152.3
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}


Star "BY Dra A/HIP 91009/HD 234677"
{
	ParentBody "BY Dra (AB)"
	Class      "K5 V" //Pre-Main Squence star
	Radius     835200
	AppMagn    8.07
	Orbit
	{
		Period          143.4027
		SemiMajorAxis   0.0342
		Eccentricity    0.30014
		Inclination     154.41
		AscendingNode   152.3
		ArgOfPericenter 230.33
		Epoch           2453999.2144
		MeanAnomaly     0
	}
}

Star "BY Dra B"
{
	ParentBody "BY Dra (AB)"
	Class      "K7 V" //Pre-Main Squence star
	Orbit
	{
		Period          143.4027
		SemiMajorAxis   0.0389
		Eccentricity    0.30014
		Inclination     154.41
		AscendingNode   152.3
		ArgOfPericenter 50.33
		Epoch           2453999.2144
		MeanAnomaly     0
	}
}

Star "BY Dra C"
{
	ParentBody "BY Dra"
	Class      "M5 V"
	Radius     104400
	Orbit
	{
		Period          2880.73
		SemiMajorAxis   246.4455 //unknown related with period
		Inclination     154.41 //AN and IN unknown just aligned with ab     Orbit
		AscendingNode   152.3
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//CM Dra;WD STAR PRESENT

//29 Dra;WD STAR PRESENT

Star	"Struve 2398 A/Gliese 725 A"
{
	ParentBody  "Struve 2398"
	Class       "M3V"
	AbsMagn     11.20
	MassSol     0.36
	Radius      382800
	Temperature 3680
	FeH        -0.49

	Orbit
	{
		Epoch          2370461.5
		Period         294.7
		SemiMajorAxis  19.09	// 42 * mass ratio
		Eccentricity   0.7
		Inclination    52.5
		AscendingNode  139.9
		ArgOfPericen   23.8
		MeanAnomaly    0
	}
}

Star	"Struve 2398 B/Gliese 725 B"
{
	ParentBody  "Struve 2398"
	Class       "M3V"
	AbsMagn     11.96
	MassSol     0.30
	Radius      375840
	Temperature 3000
	FeH        -0.49

	Orbit
	{
		Epoch          2370461.5
		Period         294.7
		SemiMajorAxis  22.91	// 42 * mass ratio
		Eccentricity   0.7
		Inclination    52.5
		AscendingNode  139.9
		ArgOfPericen   203.8
		MeanAnomaly    0
	}
}

/////////////////////////ERIDANUS/////////////////////////////////


//Keid;

Star	"Keid A/OMI2 Eri A/40 Eri A/Gliese 166 A/HIP 19849/HD 26965/SAO 131063/HR 1325"
{
	ParentBody  "Keid"
	Class       "K1V"
	AppMagn     4.42
	MassSol     0.86
	RadSol      0.81
	Teff        5300
	FeH         -0.19
	Age         5.6

	Orbit
	{
		Epoch           24517299.6	// taken from BC pair
		Period          8000
		SemiMajorAxis   180		// mass ratio * 400 AU
		Eccentricity    0.4		// random
		Inclination     108.9	// taken from BC pair
		AscendingNode   150.9	// taken from BC pair
		ArgOfPericenter 147.8	// taken from BC pair
		MeanAnomaly     0
	}
}

Barycenter	"Keid (BC)/OMI2 Eri (BC)/40 Eri (BC)/Gliese 166 (BC)"
{
	ParentBody  "Keid"
	MassSol     0.7

	Orbit
	{
		Epoch           24517299.6	// taken from BC pair
		Period          8000
		SemiMajorAxis   220		// mass ratio * 400 AU
		Eccentricity    0.4		// random
		Inclination     108.9	// taken from BC pair
		AscendingNode   150.9	// taken from BC pair
		ArgOfPericenter 327.8	// taken from BC pair
		MeanAnomaly     0
	}
}

Star	"Keid B/OMI2 Eri B/40 Eri B/Gliese 166 B/HD 26976/SAO 131065"
{
	ParentBody  "Keid (BC)"
	Class       "DA4"
	AppMagn     9.52
	MassSol     0.5
	RadSol      0.014
	Teff        16500

	Orbit
	{
		Epoch           24517299.6
		Period          252.1
		SemiMajorAxis   10	// mass ratio * 35 AU
		Eccentricity    0.41
		Inclination     108.9
		AscendingNode   150.9
		ArgOfPericenter 147.8
		MeanAnomaly     0
	}
}

Star	"Keid C/OMI2 Eri C/40 Eri C/Gliese 166 C/DY Eri"
{
	ParentBody  "Keid (BC)"
	Class       "M4.5V"
	AppMagn     11.17
	MassSol     0.2
	RadSol      0.31
	Teff        3100

	Orbit
	{
		Epoch           24517299.6
		Period          252.1
		SemiMajorAxis   25	// mass ratio * 35 AU
		Eccentricity    0.41
		Inclination     108.9
		AscendingNode   150.9
		ArgOfPericenter 327.8
		MeanAnomaly     0
	}
}

//TET Eri; english wiki

Star "TET1 Eri/HR 897/HIP 13847/HD 18622"
{
	ParentBody "TET Eri"
	Class      "A4 V"
	Radius     11136000
	AppMagn    3.2
	MassSol    2.6
	Orbit
	{
		Period          3704.4865
		SemiMajorAxis   196.7558
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "TET2 Eri/HR 898"
{
	ParentBody "TET Eri"
	Class      "A1 V"
	AppMagn    4.3
	MassSol    2.4
	Orbit
	{
		Period          3704.4865
		SemiMajorAxis   213.1521
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//p Eri;6thCVB, english wiki

Star "p Eri A/HIP 7751/HD 10361"
{
	ParentBody "p Eri"
	Class      "K0 V"
	AppMagn    5.78
	MassSol    0.87 //unknown distribution, total is 1.74 MS
	Orbit
	{
		Period          483.66
		SemiMajorAxis   30.5722
		Eccentricity    0.5344
		Inclination     142.824
		AscendingNode   13.116
		ArgOfPericenter 18.374
		Epoch           2383424.671872
		MeanAnomaly     0
	}
}

Star "p Eri B"
{
	ParentBody "p Eri"
	Class      "K5 V"
	AppMagn    5.9
	MassSol    0.87
	Orbit
	{
		Period          483.66
		SemiMajorAxis   30.5722
		Eccentricity    0.5344
		Inclination     142.824
		AscendingNode   13.116
		ArgOfPericenter 198.374
		Epoch           2383424.671872
		MeanAnomaly     0
	}
}

//The Astronomical Journal, 134:1769Y1776, 2007 November
//TERNARITY, ACTIVITY, AND EVOLUTIONARY STATE OF THE W UMaYTYPE BINARY UX ERIDANI
//S.-B. Qian,J.-Z. Yuan,F.-Y. Xiang,B. Soonthornthum,L.-Y. Zhu and J.-J. He
//6thCVB, simbad database
//AB overcontacting binary


Barycenter "UX Eri (AB)"
{
	ParentBody "UX Eri"
	Orbit
	{
		Period          42.8
		SemiMajorAxis   4.9099
		Eccentricity    0.6
		Inclination     44.5
		ArgOfPericenter 61.3
		Epoch           2441296
		MeanAnomaly     0
	}
}

Star "UX Eri A/HIP 14699"
{
	ParentBody "UX Eri (AB)"
	Class      "F9 V"
	Radius     633360
	AppMagn    10.639
	MassSol    0.54
	Orbit
	{
		Period          0.00121995
		SemiMajorAxis   0.0105
		Inclination     75.32
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "UX Eri B"
{
	ParentBody "UX Eri (AB)"
	Class      "F V"
	Radius     1009200
	MassSol    1.45
	Orbit
	{
		Period          0.00121995
		SemiMajorAxis   0.0039
		Inclination     75.32
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "UX Eri C"
{
	ParentBody "UX Eri"
	Class      "M V"  //related with Mass, could be also a white dwarf
	MassSol    0.56
	Orbit
	{
		Period          42.8
		SemiMajorAxis   17.4478
		Eccentricity    0.6
		Inclination     44.5
		ArgOfPericenter 241.3
		Epoch           2441296
		MeanAnomaly     0
	}
}


//63 Eri;WD STAR PRESENT

/////////////////////////CARINA////////////////////////////////////////


//UPS Car; english wiki

Star "UPS Car A/HIP 48002/HD 85124/HR 3891"
{
	ParentBody "UPS Car"
	Class      "A8 Ib"
	AppMagn    3.08
	MassSol    13
	Orbit
	{
		Period          19500
		SemiMajorAxis   761.9048
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "UPS Car B/HD 85123/HR 3890"
{
	ParentBody "UPS Car"
	Class      "B7 III"
	AppMagn    6.25
	MassSol    8
	Orbit
	{
		Period          19500
		SemiMajorAxis   1238.0952
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//WR 43;english wiki

Star "NGC 3603-A1 A/WR 43 A/HD 97950A1"
{
	ParentBody "NGC 3603-A1"
	Class      "WN6"
	Radius     20184000
	AppMagn    11.18
	MassSol    120
	Orbit
	{
		Period          0.01033534
		SemiMajorAxis   0.1229
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "NGC 3603-A1 B/WR 43 B"
{
	ParentBody "NGC 3603-A1"
	Class      "WN6"
	Radius     18096000
 
	MassSol    92
	Orbit
	{
		Period          0.01033534
		SemiMajorAxis   0.1603
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//EM Car; spanish wiki

Star "EM Car A/HD 97484"
{
	ParentBody "EM Car"
	Class      "O8 V"
	Radius     6507600
	AppMagn    8.52
	MassSol    23.3
	Orbit
	{
		Period          0.00935425
		SemiMajorAxis   0.0773
		Eccentricity    0.012
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "EM Car B"
{
	ParentBody "EM Car"
	Class      "O8 V"
	Radius     5853360
	MassSol    21.8
	Orbit
	{
		Period          0.00935425
		SemiMajorAxis   0.0827
		Eccentricity    0.012
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//QX Car; spanish wiki

Star "QX Car A/HD 86118"
{
	ParentBody "QX Car"
	Class      "B2 V"
	Radius     2992800
	AppMagn    6.64
	MassSol    9.25
	Orbit
	{
		Period          0.01227397
		SemiMajorAxis   0.067
		Eccentricity    0.28
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "QX Car B"
{
	ParentBody "QX Car"
	Class      "B2 V"
	Radius     2853600
	MassSol    8.5
	Orbit
	{
		Period          0.01227397
		SemiMajorAxis   0.073
		Eccentricity    0.28
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//V415 Car; 6thCVB and spanish wiki
//eclipsing binary, 82 sky plane

Star "V415 Car A/HIP 32761/HD 50337"
{
	ParentBody "V415 Car"
	Class      "G6 II"
	Radius     21784800
	AppMagn    4.42
	MassSol    4.3
	Orbit
	{
		Period          0.535
		SemiMajorAxis   0.1139
		Inclination     101.32
		AscendingNode   141.16
		ArgOfPericenter 0
		Epoch           2447849.75
		MeanAnomaly     0
	}
}

Star "V415 Car B"
{
	ParentBody "V415 Car"
	Class      "A1 V"
	Radius     1322400 
	MassSol    2
	Orbit
	{
		Period          0.535
		SemiMajorAxis   0.2449
		Inclination     101.32
		AscendingNode   141.16
		ArgOfPericenter 180
		Epoch           2447849.75
		MeanAnomaly     0
	}
}

//WR 20a; spanish wiki

Star "WR 20a A"
{
	ParentBody "WR 20a"
	Class      "WN6"
	Radius     8978400  //unknown, standard for a WN6 star; too small in SE
	AppMagn    13.45
	MassSol    83
	Orbit
	{
		Period          0.01009863
		SemiMajorAxis   0.1275
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "WR 20a B"
{
	ParentBody "WR 20a"
	Class      "WN6"
	Radius     8978400 //unknown, standard for a WN6 star; too small in SE
	MassSol    82
	Orbit
	{
		Period          0.01009863
		SemiMajorAxis   0.1291
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//WR 22; spanish wiki

Star "WR 22 A/HIP 52308/HD 4188"
{
	ParentBody "WR 22"
	Class      "WN7"
	Radius     13920000
	AppMagn    6.42
	MassSol    72
	Orbit
	{
		Period          0.22
		SemiMajorAxis   0.4419
		Eccentricity    0.56
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "WR 22 B"
{
	ParentBody "WR 22"
	Class      "O9 V"
	Radius     7656000
 
	MassSol    25.7
	Orbit
	{
		Period          0.22
		SemiMajorAxis   1.2381
		Eccentricity    0.56
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//Achird; 6thCVB, english wiki
//very good system

Star "ETA1 Cas/ETA Cas A/HIP 3821/HD 4614"
{
	ParentBody "Achird"
	Class      "G0 V"
	Radius     722865.6
	AppMagn    3.52
	MassSol    0.972
	Orbit
	{
		Period          480
		SemiMajorAxis   26.4109
		Eccentricity    0.497
		Inclination     34.76
		AscendingNode   278.42
		ArgOfPericenter 268.59
		Epoch           2411221.794653
		MeanAnomaly     0
	}
}

Star "ETA2 Cas/ETA Cas B"
{
	ParentBody "Achird"
	Class      "K7 V"
	Radius     459360
	AppMagn    7.36
	MassSol    0.57
	Orbit
	{
		Period          480
		SemiMajorAxis   45.0375
		Eccentricity    0.497
		Inclination     34.76
		AscendingNode   278.42
		ArgOfPericenter 88.59
		Epoch           2411221.794653
		MeanAnomaly     0
	}
}

//IOT Cas; 6thCVB, jim kaller stars website, english and spanish wiki
//AB-C     Orbit in 6thCVB with erroneous eccentricity data (8.1??), also could be
//that B is not bounded to the system


Barycenter "IOT Cas (AB)"
{
	ParentBody "IOT Cas"
	Orbit
	{
		Period          2625.3
		SemiMajorAxis   85.5163
		Inclination     87.6
		AscendingNode   109.82
		ArgOfPericenter 8.82
		Epoch           2542855.083093
		MeanAnomaly     0
	}
}


Barycenter "IOT Cas C"
{
	ParentBody "IOT Cas"
	Orbit
	{
		Period          2625.3
		SemiMajorAxis   235.907
		Inclination     87.6
		AscendingNode   109.82
		ArgOfPericenter 188.82
		Epoch           2542855.083093
		MeanAnomaly     0
	}
}

Barycenter "IOT Cas A"
{
	ParentBody "IOT Cas (AB)"
	Orbit
	{
		Period          620
		SemiMajorAxis   40.6557
		Eccentricity    0.75
		Inclination     115
		AscendingNode   0.8
		ArgOfPericenter 283
		Epoch           2320057.341837
		MeanAnomaly     0
	}
}

Star "IOT Cas Aa/HIP 11569/HD 15089"
{
	ParentBody "IOT Cas A"
	Class      "A3 V"
	AppMagn    4.63
	MassSol    2
	Orbit
	{
		Period          47.05
		SemiMajorAxis   4.6959
		Eccentricity    0.626
		Inclination     149.2
		AscendingNode   176.7
		ArgOfPericenter 328.3
		Epoch           2449068.19129
		MeanAnomaly     0
	}
}

Star "IOT Cas Ab"
{
	ParentBody "IOT Cas A"
	Class      "G2 V"
	AppMagn    8.48
	MassSol    0.7
	Orbit
	{
		Period          47.05
		SemiMajorAxis   13.4168
		Eccentricity    0.626
		Inclination     149.2
		AscendingNode   176.7
		ArgOfPericenter 148.3
		Epoch           2449068.19129
		MeanAnomaly     0
	}
}

Star "IOT Cas B"
{
	ParentBody "IOT Cas (AB)"
	Class      "F5 V"
	AppMagn    6.89
	MassSol    1.3
	Orbit
	{
		Period          620
		SemiMajorAxis   84.4388
		Eccentricity    0.75
		Inclination     115
		AscendingNode   0.8
		ArgOfPericenter 103
		Epoch           2320057.341837
		MeanAnomaly     0
	}
}

Star "IOT Cas Ca"
{
	ParentBody "IOT Cas C"
	Class      "G7 V"
	AppMagn    8.4
	MassSol    0.8
	Orbit
	{
		Period          30.96
		SemiMajorAxis   7.7884
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}


Star "IOT Cas Cb"
{
	ParentBody "IOT Cas C"
	Class      "K5 V"
	MassSol    0.65
	Orbit
	{
		Period          30.96
		SemiMajorAxis   9.5858
		ArgOfPericenter 180 
		MeanAnomaly     0
	}
}

//Ksora;Eclipsing binary, english wiki
//just observed eclipsing period

Star "Ksora A/DEL Cas A/HIP 6686/HD 8538"
{
	ParentBody "Ksora"
	Class      "A5 III"
	Radius     2714400
	AppMagn    2.68
	MassSol    2.5
	Orbit
	{
		Period          2.05479452
		SemiMajorAxis   1.3947
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Ksora B/DEL Cas B"
{
	ParentBody "Ksora"
	AppMagn    6 //unknown
	Orbit
	{
		Period          2.05479452
		SemiMajorAxis   1.3947
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//LAM Cas;6thCVB, spanish wiki
//very good system

Star "LAM Cas A/HIP 2505/HD 2772"
{
	ParentBody "LAM Cas"
	Class      "B8 V"
	Radius     1948800
	AppMagn    5.33
	MassSol    3
	Orbit
	{
		Period          536.47
		SemiMajorAxis   65.2124
		Eccentricity    0.816
		Inclination     75.8
		AscendingNode   17.4
		ArgOfPericenter 260.5
		Epoch           2455960.311581
		MeanAnomaly     0
	}
}

Star "LAM Cas B"
{
	ParentBody "LAM Cas"
	Class      "B9 V"
	Radius     1879200
	AppMagn    5.62
	MassSol    2.8
	Orbit
	{
		Period          536.47
		SemiMajorAxis   69.8704
		Eccentricity    0.816
		Inclination     75.8
		AscendingNode   17.4
		ArgOfPericenter 80.5
		Epoch           2455960.311581
		MeanAnomaly     0
	}
}

//MU Cas; 6thCVB, english wiki
//very good system

Star "MU Cas A/HIP 5336/HD 6582"
{
	ParentBody "MU Cas"
	Class      "G5 VI"
	Radius     550536
	AppMagn    5.17
	MassSol    0.74
	Orbit
	{
		Period          21.753
		SemiMajorAxis   1.4224
		Eccentricity    0.561
		Inclination     106.8
		AscendingNode   47.3
		ArgOfPericenter 152.7
		Epoch           2442683.027171
		MeanAnomaly     0
	}
}

Star "MU Cas B"
{
	ParentBody "MU Cas"
	Class      "M5 V"
	Radius     201840
	AppMagn    10.7
	MassSol    0.17
	Orbit
	{
		Period          21.753
		SemiMajorAxis   6.1915
		Eccentricity    0.561
		Inclination     106.8
		AscendingNode   47.3
		ArgOfPericenter 332.7
		Epoch           2442683.027171
		MeanAnomaly     0
	}
}

//OMI Cas;Aa/Ab     Orbits 6thCVB, english wiki

Barycenter "OMI Cas A"
{
	ParentBody "OMI Cas"
	Orbit
	{
		Period          237063.79
		SemiMajorAxis   897.7707
		Inclination     115  //IN and AN jus aligned with AaAb
		AscendingNode   87.3
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "OMI Cas Aa/HIP 3504/HD 4180"
{
	ParentBody "OMI Cas A"
	Class      "B5 III"
	AppMagn    4.54
	Orbit
	{
		Period          2.83712329
		SemiMajorAxis   2.3727
		Inclination     115
		AscendingNode   87.3
		ArgOfPericenter 0
		Epoch           2452792.2
		MeanAnomaly     0
	}
}

Star "OMI Cas Ab"
{
	ParentBody "OMI Cas A"
	Class      "B V" //unknown, related with     AppMagn
	AppMagn    7.5
	Orbit
	{
		Period          2.83712329
		SemiMajorAxis   2.3727
		Inclination     115
		AscendingNode   87.3
		ArgOfPericenter 180
		Epoch           2452792.2
		MeanAnomaly     0
	}
}

Star "OMI Cas B"
{
	ParentBody "OMI Cas"
	Class      "F V"
	AppMagn    11
	Orbit
	{
		Period          237063.79
		SemiMajorAxis   8481.3704 //just observed separation
		Inclination     115  //IN and AN jus aligned with AaAb
		AscendingNode   87.3
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//SIG Cas; english and spanish wiki

Star "SIG Cas A/HIP 118243/HD 224572"
{
	ParentBody "SIG Cas"
	Class      "B III"  //giant
	AppMagn    5
	Orbit
	{
		Period          94463.49045043
		SemiMajorAxis   2377.3006
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "SIG Cas B"
{
	ParentBody "SIG Cas"
	Class      "B V" //seems to be in the main sequence
	AppMagn    7.1
	Orbit
	{
		Period          94463.49045043
		SemiMajorAxis   2377.3006
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//21 Cas; english, spanish wiki

Barycenter "21 Cas (AB)"
{
	ParentBody "21 Cas"
	Orbit
	{
		Period          86580
		SemiMajorAxis   574.7382
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "21 Cas A/YZ Cas/HIP 35721/HD 4161"
{
	ParentBody "21 Cas (AB)"
	Class      "A1 V"
	Radius     1767840
	AppMagn    4.65
	MassSol    2.32
	Orbit
	{
		Period          0.0122389
		SemiMajorAxis   0.0302
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "21 Cas B"
{
	ParentBody "21 Cas (AB)"
	Class      "F2 V"
	Radius     939600
	MassSol    1.35
	Orbit
	{
		Period          0.0122389
		SemiMajorAxis   0.0519
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "TYC 4307-2168-1/21 Cas C/HD 4161 B"
{
	ParentBody "21 Cas"
	Class      "K V" //unknown, related with confirmed Mass
	AppMagn    9.7
	MassSol    0.8
	Orbit
	{
		Period          86580
		SemiMajorAxis   2636.6115
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//Pearce's Star; spanish and english wiki


Star "Pearce Star A/AO Cas A/HIP 1415/HD 1337"
{
	ParentBody "Pearce Star"
	Class      "O8 III"
	Radius     16008000
	AppMagn    6.1
	MassSol    32
	Orbit
	{
		Period          0.00958904
		SemiMajorAxis   0.0923
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Pearce Star B/AO Cas B"
{
	ParentBody "Pearce Star"
	Class      "O9 III"
	Radius     10440000
	MassSol    30
	Orbit
	{
		Period          0.00958904
		SemiMajorAxis   0.0985
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//RZ Cas; spanish wiki
//2 more companions but with too few data

Star "RZ Cas A/HIP 13133/HD 17138"
{
	ParentBody "RZ Cas"
	Class      "A3 V"
	Radius     1050960
	AppMagn    6.26
	MassSol    2.03
	Orbit
	{
		Period          0.00327466
		SemiMajorAxis   0.0079
		ArgOfPericenter 0
 
		MeanAnomaly     0
	}
}

Star "RZ Cas B"
{
	ParentBody "RZ Cas"
	Class      "K0 IV"
	Radius     1343280
	MassSol    0.7
	Orbit
	{
		Period          0.00327466
		SemiMajorAxis   0.0229
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//The Astronomical Journal, 128:2997–3004, 2004 December
//THE PHYSICAL NATURE AND     OrbitAL BEHAVIOR OF V523 CASSIOPEIAE
//Ronald G. Samec,Danny R. Faulkner and David B. Williams
//SIMBAD,6thCVB
//contacting binary


Barycenter "V523 Cas A"
{
	ParentBody "V523 Cas"
	Orbit
	{
		Period          101
		SemiMajorAxis   1.7019
		Eccentricity    0.08
		Inclination     83.8
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "V523 Cas Aa"
{
	ParentBody "V523 Cas A"
	Class      "K4 V"
	Radius     542880
	AppMagn    10.87
	MassSol    0.78
	Orbit
	{
		Period          0.00064022
		SemiMajorAxis   0.0027
		Inclination     85.39
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "V523 Cas Ab"
{
	ParentBody "V523 Cas A"
	Class      "M V"
	Radius     403680
	MassSol    0.4
	Orbit
	{
		Period          0.00064022
		SemiMajorAxis   0.0052
		Inclination     85.39
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "V523 Cas B"
{
	ParentBody "V523 Cas"
	Class      "M V"
	MassSol    0.41
	Orbit
	{
		Period          101
		SemiMajorAxis   4.8981
		Eccentricity    0.08
		Inclination     83.8
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//38 Cas;6thCVB
//NEW PRECISION Orbits OF BRIGHT DOUBLE-LINED SPECTROSCOPIC BINARIES. VII.
//47 ANDROMEDAE, 38 CASSIOPEIAE, AND HR 8467
//Francis C. Fekel1, Jocelyn Tomkin, Michael H. Williamson, and Dimitri Pourbaix


Star "38 Cas A/HIP 7078/HD 9021"
{
	ParentBody "38 Cas"
	Class      "F6 V"
	AppMagn    5.83
	MassSol    1.25
	Orbit
	{
		Period          0.3674
		SemiMajorAxis   0.1152
		Eccentricity    0.31
		Inclination     85.6
		AscendingNode   160.5
		ArgOfPericenter 188.2
		Epoch           2429000.4
		MeanAnomaly     0
	}
}

Star "38 Cas B"
{
	ParentBody "38 Cas"
	AppMagn    11 //unknown,SP companion
	Orbit
	{
		Period          0.3674
		SemiMajorAxis   0.1152
		Eccentricity    0.31
		Inclination     85.6
		AscendingNode   160.5
		ArgOfPericenter 8.2
		Epoch           2429000.4
		MeanAnomaly     0
	}
}

///////////////////////////PUPPIS//////////////////////////////////////////////


//Tau Pup; 6thCVB, spanish wiki
//Good system

Star "TAU Pup A/HIP 32768/HD 50310"
{
	ParentBody "TAU Pup"
	Class      "K1 III"
	Radius     18096000
	AppMagn    2.93
	MassSol    3.3
	Orbit
	{
		Period          2.9205
		SemiMajorAxis   0.0228
		Eccentricity    0.09
		Inclination     80.2
		AscendingNode   2.9
		ArgOfPericenter 64
		Epoch           2420992.8
		MeanAnomaly     0
	}
}

Star "TAU Pup B"
{
	ParentBody "TAU Pup"
	Class      "M V"
	Orbit
	{
		Period          2.9205
		SemiMajorAxis   0.3764
		Eccentricity    0.09
		Inclination     80.2
		AscendingNode   2.9
		ArgOfPericenter 244
		Epoch           2420992.8
		MeanAnomaly     0
	}
}

//2 Pup; spanish wiki, SIMBAD
//PV pup eclipsing binary


Barycenter "2 Pup B"
{
	ParentBody "2 Pup"
	Orbit
	{
		Period          30000
		SemiMajorAxis   764.5531
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "2 Pup Ba/PV Pup/HD 62863/HR 3009"
{
	ParentBody "2 Pup B"
	Class      "A8 V"
	Radius     1071840
	AppMagn    6.93
	MassSol    1.57
	Orbit
	{
		Period          0.00454986
		SemiMajorAxis   0.0199
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "2 Pup Bb"
{
	ParentBody "2 Pup B"
	Class      "A8 V"
	Radius     1044000
	MassSol    1.55
	Orbit
	{
		Period          0.00454986
		SemiMajorAxis   0.0201
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "2 Pup A/HD 62864/HR 3010"
{
	ParentBody "2 Pup"
	Class      "A2 V"
	AppMagn    6.04
	MassSol    2.5
	Orbit
	{
		Period          30000
		SemiMajorAxis   954.1623
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//5 Pup;6thCVB, spanish wiki, celestia for distance
//I used the 2nd theory for     Orbit param in the 6thCVB
//seems to be more accurative with     MassSoles and spectral     Class
//good system

Star "5 Pup A/HIP 38048/HD 63336"
{
	ParentBody "5 Pup"
	Class      "F5 V"
	AppMagn    5.73
	MassSol    1.35
	Orbit
	{
		Period          1331.6
		SemiMajorAxis   78.0975
		Eccentricity    0.401
		Inclination     97.8
		AscendingNode   17.8
		ArgOfPericenter 345.1
		Epoch           2390329.940882
		MeanAnomaly     0
	}
}

Star "5 Pup B"
{
	ParentBody "5 Pup"
	Class      "G3 V"
	AppMagn    7.32
	Orbit
	{
		Period          1331.6
		SemiMajorAxis   87.5587
		Eccentricity    0.401
		Inclination     97.8
		AscendingNode   17.8
		ArgOfPericenter 165.1
		Epoch           2390329.940882
		MeanAnomaly     0
	}
}

//18 Pup;spanish wiki

Star "18 Pup A/GJ 9255 A/HIP 40035/HD 68146"
{
	ParentBody "18 Pup"
	Class      "F6 V"
	Radius     904800
	AppMagn    5.54
	MassSol    1.17
	Orbit
	{
		Period          74689.57
		SemiMajorAxis   524.872
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "18 Pup B/GJ 9255 B"
{
	ParentBody "18 Pup"
	Class      "M2 V"
	AppMagn    11.8
	MassSol    0.4
	Orbit
	{
		Period          74689.57
		SemiMajorAxis   1535.2507
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//Gliese 283;WD STAR PRESENT

//Gliese 9223; spanish wiki


Barycenter "Gliese 9223 (AB)"
{
	ParentBody "Gliese 9223"
	Orbit
	{
		Period          108316.51
		SemiMajorAxis   868.5318
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Gliese 9223 A/HIP 34065/HD 53705"
{
	ParentBody "Gliese 9223 (AB)"
	Class      "G0 V"
	Radius     793440
	AppMagn    5.54
	MassSol    0.93
	Orbit
	{
		Period          4545
		SemiMajorAxis   153.8222
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Gliese 9223 B/HD 53706"
{
	ParentBody "Gliese 9223 (AB)"
	Class      "K0 V"
	Radius     549840
	AppMagn    6.86
	MassSol    0.81
	Orbit
	{
		Period          4545
		SemiMajorAxis   176.6107
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "Gliese 9223 C/HD 53680"
{
	ParentBody "Gliese 9223"
	Class      "K6 V"
	AppMagn    8.8
	MassSol    0.69
	Orbit
	{
		Period          108316.51
		SemiMajorAxis   2190.2106
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//V595 Pup; spanish wiki

Barycenter "V596 Pup A"
{
	ParentBody "V596 Pup"
	Orbit
	{
		Period          370
		SemiMajorAxis   25.8372
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "V596 Pup Aa/HD 71581"
{
	ParentBody "V596 Pup A"
	Class      "A1 V"
	Radius     1503360
	AppMagn    6.59
	MassSol    2.1
	Orbit
	{
		Period          0.01259233
		SemiMajorAxis   0.0437
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "V596 Pup Ab"
{
	ParentBody "V596 Pup A"
	Class      "A1 V"
	Radius     1503360
	MassSol    2.1
	Orbit
	{
		Period          0.01259233
		SemiMajorAxis   0.0437
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "V596 Pup B"
{
	ParentBody "V596 Pup"
	Class      "A6 V"
	AppMagn    7.9
	MassSol    1.62
	Orbit
	{
		Period          370
		SemiMajorAxis   66.9853
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//WD 0751-252;WD STAR PRESENT

//SIG Pup;6thCVB,english and spanish wiki

Barycenter "SIG Pup (AB)"
{
	ParentBody "SIG Pup"
	Orbit
	{
		Period          27000
		SemiMajorAxis   152.9412
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "SIG Pup A/HIP 36377/HD 59717"
{
	ParentBody "SIG Pup (AB)"
	Class      "K4 III"
	Radius     74472000 //english wiki data fits better for     Radius
	AppMagn    3.25
	MassSol    5
	Orbit
	{
		Period          0.70581793
		SemiMajorAxis   0.6638 //total semiaxis 4 times size of A in english wiki
		Eccentricity    0.17
		Inclination     65.6
		ArgOfPericenter 349.3
		Epoch           2420418.6
		MeanAnomaly     0
	}
}

Star "SIG Pup B"
{
	ParentBody "SIG Pup (AB)"
	Class      "A2 V"
	MassSol    2.5
	Orbit
	{
		Period          0.70581793
		SemiMajorAxis   1.3275
		Eccentricity    0.17
		Inclination     65.6
		ArgOfPericenter 169.3
		Epoch           2420418.6
		MeanAnomaly     0
	}
}

Star "SIG Pup C"
{
	ParentBody "SIG Pup"
	Class      "G5 V"
	AppMagn    9.4
	Orbit
	{
		Period          27000
		SemiMajorAxis   1147.0588
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

/////////////////////////GEMINI///////////////////////////////////////////

// The Castor system data is taken from
// http://www.solstation.com/stars2/castor6.htm

Barycenter	"Castor (AB)"
{
	ParentBody  "Castor"
	MassSol     4.85

	Orbit
	{
		Period          10681.406
		SemiMajorAxis   216.7218543
		Eccentricity    0.9
		Inclination     100
		AscendingNode   56.072
		ArgOfPericenter 213.139
		MeanAnomaly     60
	}
}

Barycenter	"Castor C/Y Gem/ALF Gem C/66 Gem C/HD 60179 C/SAO 60199"
{
	ParentBody  "Castor"
	Lum         0.051
	MassSol     1.19

	Orbit
	{
		Period          10681.406
		SemiMajorAxis   883.2781458
		Eccentricity    0.9
		Inclination     100
		AscendingNode   56.072
		ArgOfPericenter 33.139
		MeanAnomaly     60
	}
}

Barycenter	"Castor A/ALF Gem A/66 Gem A/HD 60179"
{
	ParentBody  "Castor (AB)"
	Lum         34
	MassSol     2.65

	Orbit
	{
		Period          467
		SemiMajorAxis   48.53608247
		Eccentricity    0.343
		Inclination     114.5
		AscendingNode   56.072
		ArgOfPericenter 213.139
		MeanAnomaly     60
	}
}

Barycenter	"Castor B/ALF Gem B/66 Gem B/HD 60178"
{
	ParentBody  "Castor (AB)"
	Lum         14
	MassSol     2.2

	Orbit
	{
		Period          467
		SemiMajorAxis   48.53608247
		Eccentricity    0.343
		Inclination     114.5
		AscendingNode   56.072
		ArgOfPericenter 33.139
		MeanAnomaly     60
	}
}

Star	"Castor Aa"
{
	ParentBody  "Castor A"
	Class       "A2V"
	Lum         33.99
	MassSol     2.15
	Radius      1600800
	Age         0.2

	Orbit
	{
		Period          0.025215606
		SemiMajorAxis   0.041509434
		Eccentricity    0.499
		Inclination     100
		AscendingNode   56.072
		ArgOfPericenter 213.139
		MeanAnomaly     60
	}
}

Star	"Castor Ab"
{
	ParentBody  "Castor A"
	Class       "M5V"
	Lum         0.01
	MassSol     0.5
	Radius      552416
	Age         0.2

	Orbit
	{
		Period          0.025215606
		SemiMajorAxis   0.178490566
		Eccentricity    0.499
		Inclination     100
		AscendingNode   56.072
		ArgOfPericenter 33.139
		MeanAnomaly     60
	}
}

Star	"Castor Ba"
{
	ParentBody  "Castor B"
	Class       "A3V"
	Lum         13.98
	MassSol     1.7
	Radius      1113600
	Age         0.2

	Orbit
	{
		Period          0.008021903
		SemiMajorAxis   0.006818182
		Eccentricity    0.01
		Inclination     100
		AscendingNode   56.072
		ArgOfPericenter 213.139
		MeanAnomaly     60
	}
}

Star	"Castor Bb"
{
	ParentBody  "Castor B"
	Class       "M2V"
	Lum         0.02
	MassSol     0.5
	Radius      500000
	Age         0.2

	Orbit
	{
		Period          0.008021903
		SemiMajorAxis   0.023181818
		Eccentricity    0.01
		Inclination     100
		AscendingNode   56.072
		ArgOfPericenter 33.139
		MeanAnomaly     60
	}
}

Star	"Castor Ca"
{
	ParentBody  "Castor C"
	Class       "M0V"
	Lum         0.026
	MassSol     0.62
	Radius      528960
	Age         0.2

	Orbit
	{
		Period          0.002228658
		SemiMajorAxis   0.008621849
		Eccentricity    0
		Inclination     100
		AscendingNode   56.072
		ArgOfPericenter 213.139
		MeanAnomaly     60
	}
}

Star	"Castor Cb"
{
	ParentBody  "Castor C"
	Class       "M0V"
	Lum         0.025
	MassSol     0.57
	Radius      473280
	Age         0.2

	Orbit
	{
		Period          0.002228658
		SemiMajorAxis   0.009378151
		Eccentricity    0
		Inclination     100
		AscendingNode   56.072
		ArgOfPericenter 33.139
		MeanAnomaly     60
	}
}


//Alhena; 6thCVB, eng wiki

Star "Alhena A/GAM Gem A/HIP 31681/HD 47105"
{
	ParentBody "Alhena"
	Class      "A0 V"
	Radius     3480000
	AppMagn    1.9
	MassSol    2.8
	Orbit
	{
		Period          12.6425
		SemiMajorAxis   0.6925
		Eccentricity    0.89
		Inclination     106.7
		AscendingNode   243.6
		ArgOfPericenter 312.6
		Epoch           2443999.1
		MeanAnomaly     0
	}
}

Star "GAM Gem B"
{
	ParentBody "Alhena"
	Class      "G V"
	AppMagn    8
	MassSol    1
	Orbit
	{
		Period          12.6425
		SemiMajorAxis   1.9389
		Eccentricity    0.89
		Inclination     106.7
		AscendingNode   243.6
		ArgOfPericenter 132.6
		Epoch           2443999.1
		MeanAnomaly     0
	}
}

//KAP Gem; spanish wiki, prof jim kaller

Star "KAP Gem A/HIP 37740/HD 62345"
{
	ParentBody "KAP Gem"
	Class      "G8 III"
	Radius     8004000
	AppMagn    3.57
	MassSol    2.7
	Orbit
	{
		Period          3039.92910741
		SemiMajorAxis   87.672
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "KAP Gem B"
{
	ParentBody "KAP Gem"
	Class      "G4 V"
	AppMagn    8.2
	Orbit
	{
		Period          3039.92910741
		SemiMajorAxis   236.7145
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


//LAM Gem; spanish wiki, prof. jim kaller
//LAM Ab, too few data

Star "LAM Gem A/HIP 35350/HD 56537"
{
	ParentBody "LAM Gem"
	Class      "A3 V"
	Radius     1670400
	AppMagn    3.58
	MassSol    2.2
	Orbit
	{
		Period          3261.72091725
		SemiMajorAxis   66.3891
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "LAM Gem B"
{
	ParentBody "LAM Gem"
	Class      "K8 V"
	AppMagn    10.4
	Orbit
	{
		Period          3261.72091725
		SemiMajorAxis   243.4268
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//NU Gem; 6thCVB,spanish wiki, prof. jim kaller
//nearby binary NU GEM B but not confirmed if they share
//the same proper motion of NU Gem A


Barycenter "NU Gem AA"
{
	ParentBody "NU Gem"
	Orbit
	{
		Period          18.75
		SemiMajorAxis   5.1971
		Eccentricity    0.297
		Inclination     72.9
		AscendingNode   120.9
		ArgOfPericenter 228.4
		Epoch           2448830.783861
		MeanAnomaly     0
	}
}

Star "NU Gem Aa1/HIP 30883/HD 45542"
{
	ParentBody "NU Gem AA"
	Class      "B6 III"
	AppMagn    4.14
	MassSol    4.6 //confirmed
	Orbit
	{
		Period          0.14717808
		SemiMajorAxis   0.0993
		Inclination     72.9   //unknown just aligned
		AscendingNode   120.9
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "NU Gem Aa2"
{
	ParentBody "NU Gem AA"
	Class      "G V" //unknown related with Mass
	MassSol    1.14   //5.74 MS for Aa1/Aa2 components
	Orbit
	{
		Period          0.14717808
		SemiMajorAxis   0.4007
		Inclination     72.9
		AscendingNode   120.9
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "NU Gem Ab"
{
	ParentBody "NU Gem"
	Class      "B V" //unknown related with Mass
	MassSol    4.1 //confirmed
	Orbit
	{
		Period          18.75
		SemiMajorAxis   7.2759
		Eccentricity    0.297
		Inclination     72.9
		AscendingNode   120.9
		ArgOfPericenter 48.4
		Epoch           2448830.783861
		MeanAnomaly     0
	}
}

//RHO Gem; eng and sp wiki


Barycenter "RHO Gem (AB)"
{
	ParentBody "RHO Gem"
	Orbit
	{
		Period          1046089.17
		SemiMajorAxis   4533.3811
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "RHO Gem A/HIP 36366/HD 58946"
{
	ParentBody "RHO Gem (AB)"
	Class      "F0 V"
	Radius     1151880
	AppMagn    4.2473
	MassSol    1.35
	Orbit
	{
		Period          370
		SemiMajorAxis   7.9264
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "RHO Gem B"
{
	ParentBody "RHO Gem (AB)"
	Class      "M5 V"
	AppMagn    12.5
	Orbit
	{
		Period          370
		SemiMajorAxis   53.5031
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "RHO Gem C/V376 Gem"
{
	ParentBody "RHO Gem"
	Class      "K2 V"
	AppMagn    7.8621
	MassSol    0.77
	Orbit
	{
		Period          1046089.17
		SemiMajorAxis   9125.6373
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//Tejat Prior;6thCVB, eng wiki, sp wiki
//unknown all Masses, but with 3rd kepler law should be around 7 MS for all system
//5.08 MS for Aa Ab pair, which I distributed 66% for Aa 33% for Ab


Barycenter "Tejat Prior A"
{
	ParentBody "Tejat Prior"
	Orbit
	{
		Period          473.7
		SemiMajorAxis   30.8309
		Eccentricity    0.54
		Inclination     142.7
		AscendingNode   84.5
		ArgOfPericenter 26.2
		Epoch           2385691.364958
		MeanAnomaly     0
	}
}

Star "Tejat Prior Aa/ETA Gem Aa/HIP 29655/HD 44995"
{
	ParentBody "Tejat Prior A"
	Class      "M3 III"
	Radius     90480000
	AppMagn    3.15
	Orbit
	{
		Period          8.2
		SemiMajorAxis   2.7559
		Inclination     142.7 //unknown just aligned with the system
		AscendingNode   84.5
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "ETA Gem Ab"
{
	ParentBody "Tejat Prior A"
	Class      "B V"
	AppMagn    3.9
	Orbit
	{
		Period          8.2
		SemiMajorAxis   4.2441
		Inclination     142.7
		AscendingNode   84.5
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "ETA Gem B"
{
	ParentBody "Tejat Prior"
	Class      "F V"
	AppMagn    8.8
	Orbit
	{
		Period          473.7
		SemiMajorAxis   85.12
		Eccentricity    0.54
		Inclination     142.7
		AscendingNode   84.5
		ArgOfPericenter 206.2
		Epoch           2385691.364958
		MeanAnomaly     0
	}
}

//Wasat;6thCVB, eng wiki, sp wiki
//unknown Masses except Aa

Barycenter "Wasat A"
{
	ParentBody "Wasat"
	Orbit
	{
		Period          1200
		SemiMajorAxis   23.2345
		Eccentricity    0.11
		Inclination     63.28
		AscendingNode   18.38
		ArgOfPericenter 57.19
		Epoch           2245913.175484
		MeanAnomaly     0
	}
}


Star "Wasat Aa/DEL Gem Aa/HIP 35550/HD 56986"
{
	ParentBody "Wasat A"
	Class      "F0 IV"
	Radius     1044000
	AppMagn    3.55
	MassSol    1.6
	Orbit
	{
		Period          6.13
		SemiMajorAxis   0.1328
		Eccentricity    0.353
		Inclination     92.4
		AscendingNode   70.04
		ArgOfPericenter 214.6
		Epoch           2415466.5
		MeanAnomaly     0
	}
}

Star "DEL Gem Ab"
{
	ParentBody "Wasat A"
	AppMagn    8 //unknown
	Orbit
	{
		Period          6.13
		SemiMajorAxis   0.1328
		Eccentricity    0.353
		Inclination     92.4
		AscendingNode   70.04
		ArgOfPericenter 34.6
		Epoch           2415466.5
		MeanAnomaly     0
	}
}

Star "DEL Gem B"
{
	ParentBody "Wasat"
	Class      "K V"
	AppMagn    8.18
	Orbit
	{
		Period          1200
		SemiMajorAxis   106.215
		Eccentricity    0.11
		Inclination     63.28
		AscendingNode   18.38
		ArgOfPericenter 237.19
		Epoch           2245913.175484
		MeanAnomaly     0
	}
}

//1 Gem;6thCVB for AB     Orbit, spanish wiki, prof. jim kaller
//4.65 total Mass, unknown distribution.
//unknown distance between Aa y Ab


Barycenter "1 Gem A"
{
	ParentBody "1 Gem"
	Orbit
	{
		Period          13.35
		SemiMajorAxis   3.3789
		Eccentricity    0.361
		Inclination     58.2
		AscendingNode   174.9
		ArgOfPericenter 198.2
		Epoch           2445072.441636
		MeanAnomaly     0
	}
}

Barycenter "1 Gem B"
{
	ParentBody "1 Gem"
	Orbit
	{
		Period          13.35
		SemiMajorAxis   5.7923
		Eccentricity    0.361
		Inclination     58.2
		AscendingNode   174.9
		ArgOfPericenter 18.2
		Epoch           2445072.441636
		MeanAnomaly     0
	}
}


Star "1 Gem Aa/HIP 28734/HD 41116"
{
	ParentBody "1 Gem A"
	Class      "G6 III"
	Radius     4315200
	AppMagn    4.7
	Orbit
	{
		Period          0.5763 //unknown
		SemiMajorAxis   0.4333 //unknown
		Inclination     58.2  //unknown, just aligned with system
		AscendingNode   174.9
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "1 Gem Ab"
{
	ParentBody "1 Gem A"
	Class      "F6 V"
	Radius     939600
	AppMagn    6.9
	Orbit
	{
		Period          0.5763 //unknown
		SemiMajorAxis   0.5667 //unknown
		Inclination     58.2  //unknown, just aligned with system
		AscendingNode   174.9
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "1 Gem Ba"
{
	ParentBody "1 Gem B"
	Class      "G8 III"
	Radius     4454400
	AppMagn    5.1
	Orbit
	{
		Period          0.0263
		SemiMajorAxis   0.0061
		Inclination     58.2  //unknown, just aligned with system
		AscendingNode   174.9
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

//spectroscopic companion

Star "1 Gem Bb"
{
	ParentBody "1 Gem B"
	AppMagn    10 //unknown
	Orbit
	{
		Period          0.0263
		SemiMajorAxis   0.1006
		Inclination     58.2  //unknown, just aligned with system
		AscendingNode   174.9
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//GX Gem;spanish wiki
//well known stars

Star "GX Gem A/HIP 32427/HD 263139"
{
	ParentBody "GX Gem"
	Class      "F7 V"
	Radius     1621680
	AppMagn    10.9
	MassSol    1.49
	Orbit
	{
		Period          0.01106849
		SemiMajorAxis   0.0348
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "GX Gem B"
{
	ParentBody "GX Gem"
	Class      "F7 V"
	Radius     1559040
	MassSol    1.47
	Orbit
	{
		Period          0.01106849
		SemiMajorAxis   0.0352
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//6thCVB

//Photoelectric radial velocities, PaperIX
//The     Orbits of the spectroscopic binaries 81 and CHI Geminorum
//R. F. Griffin, The observatories, Madingley road CB30 HA Cambridge
//12 Oct 1981


Star "CHI Gem A/HIP 39424/HD 66216"
{
	ParentBody "CHI Gem"
	Class      "K2 III"
	AppMagn    4.94
	Orbit
	{
		Period          6.6743
		SemiMajorAxis   0.3063
		Eccentricity    0.06
		Inclination     50.8
		AscendingNode   242.6
		ArgOfPericenter 264
		Epoch           2442894.5
		MeanAnomaly     0
	}
}

Star "CHI Gem B"
{
	ParentBody "CHI Gem"
	AppMagn    10 //unknown, SP companion
	Orbit
	{
		Period          6.6743
		SemiMajorAxis   1.225
		Eccentricity    0.06
		Inclination     50.8
		AscendingNode   242.6
		ArgOfPericenter 84
		Epoch           2442894.5
		MeanAnomaly     0
	}
}

//PHI Gem;6thCVB,spanish wiki

Star "PHI Gem A/HIP 38538/HD 64145"
{
	ParentBody "PHI Gem"
	Class      "A3 V"
	AppMagn    4.98
	MassSol    2.32
	Orbit
	{
		Period          1.5927
		SemiMajorAxis   0.0724
		Inclination     95.56
		AscendingNode   19.72
		ArgOfPericenter 0
		Epoch           2447820.9453
		MeanAnomaly     0
	}
}

Star "PHI Gem B"
{
	ParentBody "PHI Gem"
	AppMagn    10 //unknown
	Orbit
	{
		Period          1.5927
		SemiMajorAxis   0.336
		Inclination     95.56
		AscendingNode   19.72
		ArgOfPericenter 180
		Epoch           2447820.9453
		MeanAnomaly     0
	}
}

/////////////////////////CORVUS///////////////////////////////////////////

//DEL Cor; spanish wiki

Star "Algorab A/DEL Cor A/HIP 60965/HD 108767"
{
	ParentBody "Algorab"
	Class      "A0 V"
	Radius     1392000
	AppMagn    2.95
	MassSol    2.5
	Orbit
	{
		Period          9400
		SemiMajorAxis   142.1875
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Algorab B/DEL Cor B"
{
	ParentBody "Algorab"
	Class      "K V"
	AppMagn    8.51
	MassSol    0.7
	Orbit
	{
		Period          9400
		SemiMajorAxis   507.8125
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

/////////////////////////ARIES////////////////////////////////////////////

//Bharani; eng and sp wiki

Star "Bharani A/41 Ari A/HIP 13209/HD 17573"
{
	ParentBody "Bharani"
	Class      "B8 V"
	Radius     1809600
	AppMagn    3.61
	MassSol    3.1
	Orbit
	{
		Period          13.06336419
		SemiMajorAxis   7.638 //just observed separation
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "41 Ari B"
{
	ParentBody "Bharani"
	Class      "B V" 
	Orbit
	{
		Period          13.06336419
		SemiMajorAxis   7.638
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//sheratan; 6thCVB, english and spanish wiki

Star "Sheratan A/HIP 8903/HD 11636"
{
	ParentBody "Sheratan"
	Class      "A5 V"
	Radius     1461600
	AppMagn    2.64
	MassSol    2.34
	Orbit
	{
		Period          0.2931
		SemiMajorAxis   0.2419
		Eccentricity    0.895
		Inclination     44.7
		AscendingNode   79.1
		ArgOfPericenter 209.1
		Epoch           2444274.276
		MeanAnomaly     0
	}
}

Star "Sheratan B"
{
	ParentBody "Sheratan"
	Class      "G0 V" //if it's in the main sequence
	MassSol    1.34
	Orbit
	{
		Period          0.2931
		SemiMajorAxis   0.4225
		Eccentricity    0.895
		Inclination     44.7
		AscendingNode   79.1
		ArgOfPericenter 29.1
		Epoch           2444274.276
		MeanAnomaly     0
	}
}



//EPS Ari;6thCVB,eng & sp wiki, prof. jim kaler

Barycenter "EPS Ari (AB)"
{
	ParentBody "EPS Ari"
	Orbit
	{
		Period          800000
		SemiMajorAxis   1551.7241
		Inclination     84.2  //just aligned with system
		AscendingNode   25.6
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "EPS Ari A/HIP 13914/HD 18520"
{
	ParentBody "EPS Ari (AB)"
	Class      "A3 V"
	Radius     2505600
	AppMagn    5.2
	MassSol    2.7
	Orbit
	{
		Period          1215.913
		SemiMajorAxis   109.649
		Eccentricity    0.317
		Inclination     84.2
		AscendingNode   25.6
		ArgOfPericenter 162.1
		Epoch           1978231.185662
		MeanAnomaly     0
	}
}

Star "EPS Ari B"
{
	ParentBody "EPS Ari (AB)"
	Class      "A3 V"
	Radius     2088000
	AppMagn    5.5
	MassSol    2.5
	Orbit
	{
		Period          1215.913
		SemiMajorAxis   118.4209
		Eccentricity    0.317
		Inclination     84.2
		AscendingNode   25.6
		ArgOfPericenter 342.1
		Epoch           1978231.185662
		MeanAnomaly     0
	}
}

Star "EPS Ari C"
{
	ParentBody "EPS Ari"
	Class      "K7 V"
	AppMagn    12.7
	Orbit
	{
		Period          800000
		SemiMajorAxis   13448.2759
		Inclination     84.2  //just aligned with system
		AscendingNode   25.6
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//GAM Ari;eng,sp wiki, prof. jim kaler
//just observed separation but with a lot of other data

Star "Mesarthim A/GAM1 Ari/HIP 8832/HD 11502"
{
	ParentBody "Mesarthim"
	Class      "B9 V"
	AppMagn    4.83
	MassSol    2.8
	Orbit
	{
		Period          3510
		SemiMajorAxis   189.8368
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "GAM2 Ari"
{
	ParentBody "Mesarthim"
	Class      "A1 V"
	AppMagn    4.75
	MassSol    2.5
	Orbit
	{
		Period          3510
		SemiMajorAxis   212.6172
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//IOT Ari;WD STAR PRESENT

//LAM Ari;eng, sp wiki

Star "LAM Ari A/HIP 9153/HD 11973"
{
	ParentBody "LAM Ari"
	Class      "F0 V"
	Radius     1531200
	AppMagn    4.79
	Orbit
	{
		Period          35338.57
		SemiMajorAxis   569.2072
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "LAM Ari B"
{
	ParentBody "LAM Ari"
	Class      "G1 V"
	AppMagn    7.75
	Orbit
	{
		Period          35338.57
		SemiMajorAxis   910.7315
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//RHO3 Ari; spanish and english wiki
//spectroscopic binary

Star "RHO3 Ari A/HIP 13702/HD 18256"
{
	ParentBody "RHO3 Ari"
	Class      "F6 V"
	Radius     1531200
	AppMagn    5.63
	Orbit
	{
		Period          9.60821918  //CONFIRMED
		SemiMajorAxis   3.3281 //UNKNOWN
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "RHO3 Ari B"
{
	ParentBody "RHO3 Ari"
	AppMagn    12 //unknown, SP companion
	Orbit
	{
		Period          9.60821918
		SemiMajorAxis   3.3281
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


//35 Ari;6thCVB, eng wiki
//period and separation not according with system     MassSol

Star "35 Ari A/HIP 12719/HD 16908"
{
	ParentBody "35 Ari"
	Class      "B3 V"
	Radius     2714400
	AppMagn    4.64
	MassSol    5.7
	Orbit
	{
		Period          1.3425
		SemiMajorAxis   0.34
		Eccentricity    0.14
		Inclination     154.96
		AscendingNode   66.46
		ArgOfPericenter 320
		Epoch           2448581.4141
		MeanAnomaly     0
	}
}

Star "35 Ari B"
{
	ParentBody "35 Ari"
	AppMagn    10 //unknown, spectroscopic companion
	Orbit
	{
		Period          1.3425
		SemiMajorAxis   0.34
		Eccentricity    0.14
		Inclination     154.96
		AscendingNode   66.46
		ArgOfPericenter 140
		Epoch           2448581.4141
		MeanAnomaly     0
	}
}


//UX And;6thCVB, english wiki
//Spectral     Class      of Ab and B related with
//distribution     MassSol    in the system and     Orbit parameters



Barycenter "UX Ari A"
{
	ParentBody "UX Ari"
	Orbit
	{
		Period          111.0921
		SemiMajorAxis   8.3236
		Eccentricity    0.77
		Inclination     93.3
		AscendingNode   58.9
		ArgOfPericenter 274.9
		Epoch           2451664.9
		MeanAnomaly     0
	}
}

Star "UX Ari Aa/HIP 16042"
{
	ParentBody "UX Ari A"
	Class      "K0 IV"
	AppMagn    9.84
	MassSol    1.1
	Orbit
	{
		Period          0.01763796
		SemiMajorAxis   0.0351
		Inclination     59.2
		AscendingNode   82
		ArgOfPericenter 180
		Epoch           2450642.00075
		MeanAnomaly     0
	}
}

Star "UX Ari Ab"
{
	ParentBody "UX Ari A"
	Class      "K V"
	Orbit
	{
		Period          0.01763796
		SemiMajorAxis   0.0488
		Inclination     59.2
		AscendingNode   82
		ArgOfPericenter 360
		Epoch           2450642.00075
		MeanAnomaly     0
	}
}

Star "UX Ari B"
{
	ParentBody "UX Ari"
	Class      "K V"
	Orbit
	{
		Period          111.0921
		SemiMajorAxis   23.4801
		Eccentricity    0.77
		Inclination     93.3
		AscendingNode   58.9
		ArgOfPericenter 94.9
		Epoch           2451664.9
		MeanAnomaly     0
	}
}

//////////////////////TAURUS//////////////////////////////////////////


//Alcyone;eng and sp wiki
//B,C and D unknown hierarchy and only known separation

Barycenter "Alcyone A"
{
	ParentBody "Alcyone"
	Orbit
	{
		SemiMajorAxis   508.9916
		ArgOfPericenter 0
		MeanAnomaly     180
	}
}


Star "Alcyone Aa"
{
	ParentBody "Alcyone A"
	Class      "B7 III"
	Radius     5707200
	MassSol    3           //total of 6 ms
	AppMagn    2.873
	Orbit
	{
		Period          3.03110273
		SemiMajorAxis   1.9018
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Alcyone Ab"
{
	ParentBody "Alcyone A"
	Class      "B III"
	MassSol    3             //total of 6 ms
	Radius     5707200
	Orbit
	{
		Period          3.03110273
		SemiMajorAxis   1.9018
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "Alcyone B"
{
	ParentBody "Alcyone"
	Class      "A V"
	AppMagn    8
	MassSol    1.9
	Orbit
	{
		Period  489039.136 //unknown
		SemiMajorAxis   13846.8367
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Alcyone C"
{
	ParentBody "Alcyone"
	Class      "A V"
	AppMagn    8
	MassSol    1.9
	Orbit
	{
		Period 959390.2 //unknown
		SemiMajorAxis   21699.5974
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "Alcyone D"
{
	ParentBody "Alcyone"
	Class      "F V"
	AppMagn    8.7
	MassSol    1.32
	Orbit
	{
		Period 1041902.3 //unknown
		SemiMajorAxis   22926.5913
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

//Aldebaran;

Star "Aldebaran A/HIP 21421/HD 29139"
{
	ParentBody "Aldebaran"
	Class      "K5 III"
	Radius     30763200
	AppMagn    0.85
	MassSol    1.7
	Orbit
	{
		Period          11058.85665033
		SemiMajorAxis   49.3784
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Aldebaran B"
{
	ParentBody "Aldebaran"
	Class      "M2 V"
	Radius     250560
	AppMagn    13.5
	MassSol    0.15
	Orbit
	{
		Period          11058.85665033
		SemiMajorAxis   559.6216
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//Hyadum A;spanish, english wiki

Star "Hyadum II Aa/HIP 20455/HD 27697"
{
	ParentBody "Hyadum II"
	Class      "K0 III"
	Radius     8073600
	AppMagn    3.77
	MassSol    2.6
	Orbit
	{
		Period          1.44876712
		SemiMajorAxis   0.0955
		Eccentricity    0.42857
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Hyadum II Ab"
{
	ParentBody "Hyadum II"
	Class      "M V"
	AbsMagn    13
	Orbit
	{
		Period          1.44876712
		SemiMajorAxis   1.6545
		Eccentricity    0.42857
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//DEL3 Tau; spanish, eng wiki

Barycenter "DEL3 Tau (AB)"
{
	ParentBody "DEL3 Tau"
	Orbit
	{
		Period          106529.55
		SemiMajorAxis   699.1411
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}


Star "DEL3 Tau A/HIP 20648/HD 27962"
{
	ParentBody "DEL3 Tau (AB)"
	Class      "A IV"
	Radius     7656000
	AppMagn    4.29
	Orbit
	{
		Period          291.9987981
		SemiMajorAxis   21.1861
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "DEL3 Tau B"
{
	ParentBody "DEL3 Tau (AB)"
	Class      "G V" //unknown,related with absmag
	AppMagn    8
	Orbit
	{
		Period          291.9987981
		SemiMajorAxis   42.3722
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "DEL3 Tau C"
{
	ParentBody "DEL3 Tau"
	Class      "K V" //unknown,related with absmag
	AppMagn    11
	Orbit
	{
		Period          106529.55
		SemiMajorAxis   2796.5644
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//LAM Tau;english,spanish wiki

Barycenter "LAM Tau (AB)"
{
	ParentBody "LAM Tau"
	Orbit
	{
		Period          0.0904175222
		SemiMajorAxis   0.0209
		Eccentricity    0.15
		Inclination     71
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "LAM Tau A/HIP 18724/HD 25204"
{
	ParentBody "LAM Tau (AB)"
	Class      "B3 V"
	Radius     4454400
	AppMagn    3.41
	MassSol    7.18
	Orbit
	{
		Period          0.01083001
		SemiMajorAxis   0.0212
		Eccentricity    0.025
		Inclination     76
		ArgOfPericenter 0
		Epoch           2444667.3
		MeanAnomaly     0
	}
}

Star "LAM Tau B"
{
	ParentBody "LAM Tau (AB)"
	Class      "A4 IV"
	Radius     3688800
	MassSol    1.89
	Orbit
	{
		Period          0.01083001
		SemiMajorAxis   0.0807
		Eccentricity    0.025
		Inclination     76
		ArgOfPericenter 180
		Epoch           2444667.3
		MeanAnomaly     0
	}
}

Star "LAM Tau C"
{
	ParentBody "LAM Tau"
	Class      "M V" //unknown,it could be also a WD
	MassSol    0.5
	Orbit
	{
		Period          0.0904175222
		SemiMajorAxis   0.3791
		Eccentricity    0.15
		Inclination     71
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//OMI Tau;spanish wiki

Star "OMI Tau Aa/HIP 15900/HD 21120"
{
	ParentBody "OMI Tau"
	Class      "G6 III"
	Radius     11136000
	AppMagn    3.62
	MassSol    3.3
	Orbit
	{
		Period          4.53
		SemiMajorAxis   0.5658
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "OMI Tau Ab"
{
	ParentBody "OMI Tau"
	Class      "M V" //unknown,it could be also a white dwarf
	MassSol    0.5
	Orbit
	{
		Period          4.53
		SemiMajorAxis   3.7342
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//Rho Tau;6thCVB,spanish wiki

Star "RHO Tau A/HIP 21273/HD 28910"
{
	ParentBody "RHO Tau"
	Class      "A8 V"
	AppMagn    4.66
	MassSol    2.1
	Orbit
	{
		Period          0.2887
		SemiMajorAxis   0.0584
		Eccentricity    0.09
		Inclination     96.21
		AscendingNode   60.82
		ArgOfPericenter 0
		Epoch           2448375.8672
		MeanAnomaly     0
	}
}

Star "RHO Tau B"
{
	ParentBody "RHO Tau"
	AppMagn    10 //unknown
	Orbit
	{
		Period          0.2887
		SemiMajorAxis   0.0584
		Eccentricity    0.09
		Inclination     96.21
		AscendingNode   60.82
		ArgOfPericenter 180
		Epoch           2448375.8672
		MeanAnomaly     0
	}
}


//TET1 Tau;6thCVB,sp wiki
//very good system

Star "TET1 Tau A/HIP 20885/HD 28307"
{
	ParentBody "TET1 Tau"
	Class      "K0 III"
	Radius     8143200
	AppMagn    3.84
	MassSol    2.5
	Orbit
	{
		Period          16.26
		SemiMajorAxis   4.697
		Eccentricity    0.57
		Inclination     92.35
		AscendingNode   355.54
		ArgOfPericenter 250.1
		Epoch           2451000.322522
		MeanAnomaly     0
	}
}

Star "TET1 Tau B"
{
	ParentBody "TET1 Tau"
	Class      "F8 V"
	AppMagn    7.3
	MassSol    2.1
	Orbit
	{
		Period          16.26
		SemiMajorAxis   5.5917
		Eccentricity    0.57
		Inclination     92.35
		AscendingNode   355.54
		ArgOfPericenter 70.1
		Epoch           2451000.322522
		MeanAnomaly     0
	}
}

//TET2 Tau;6thCVB,sp wiki
//very good system


Star "TET2 Tau A/HIP 20894/HD 28319"
{
	ParentBody "TET2 Tau"
	Class      "A7 IV"
	Radius     2714400
	AppMagn    3.41
	MassSol    2.4
	Orbit
	{
		Period          0.3856
		SemiMajorAxis   0.3729
		Eccentricity    0.736
		Inclination     47.8
		AscendingNode   353.82
		ArgOfPericenter 235.41
		Epoch           2449718.848353
		MeanAnomaly     0
	}
}

Star "TET2 Tau B"
{
	ParentBody "TET2 Tau"
	Class      "A IV"
	Radius     2088000
	AppMagn    4.86
	MassSol    1.8
	Orbit
	{
		Period          0.3856
		SemiMajorAxis   0.4972
		Eccentricity    0.736
		Inclination     47.8
		AscendingNode   353.82
		ArgOfPericenter 55.41
		Epoch           2449718.848353
		MeanAnomaly     0
	}
}

//KSI Tau;6thCVB,eng and spanish wiki

Barycenter "KSI Tau (ABC)"
{
	ParentBody "KSI Tau"
	Orbit
	{
		Period          51.78
		SemiMajorAxis   3.8897
		Eccentricity    0.569
		Inclination     24.6
		AscendingNode   109.9
		ArgOfPericenter 186
		Epoch           2454616.22029
		MeanAnomaly     0
	}
}

Barycenter "KSI Tau (AB)"
{
	ParentBody "KSI Tau (ABC)"
	Orbit
	{
		Period          0.3973
		SemiMajorAxis   0.6962
		Inclination     24.6        //unknown,RA and IN just aligned
		AscendingNode   109.9
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "KSI Tau A/HIP 16083/HD 21364"
{
	ParentBody "KSI Tau (AB)"
	Class      "B8 V"
	AppMagn    3.73
	MassSol    2.5
	Orbit
	{
		Period          0.0196
		SemiMajorAxis   0.065
		Inclination     24.6    //unknown,RA and IN just aligned
		AscendingNode   109.9
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "KSI Tau B"
{
	ParentBody "KSI Tau (AB)"
	Class      "B8 V"
	MassSol    2.5
	Orbit
	{
		Period          0.0196
		SemiMajorAxis   0.065
		Inclination     24.6    //unknown,RA and IN just aligned
		AscendingNode   109.9
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "KSI Tau C"
{
	ParentBody "KSI Tau (ABC)"
	Class      "B7 V"
	MassSol    2.9
	Orbit
	{
		Period          0.3973
		SemiMajorAxis   0.4038
		Inclination     24.6    //unknown,RA and IN just aligned
		AscendingNode   109.9
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "KSI Tau D"
{
	ParentBody "KSI Tau"
	Class      "F V"
	MassSol    1.25
	Orbit
	{
		Period          51.78
		SemiMajorAxis   24.5827
		Eccentricity    0.569
		Inclination     24.6
		AscendingNode   109.9
		ArgOfPericenter 6
		Epoch           2454616.22029
		MeanAnomaly     0
	}
}

//ZET Tau;english wiki

Star "ZET Tau A/HIP 26451/HD 37202"
{
	ParentBody "ZET Tau"
	Class      "B2 III"
	Radius     3828000
	AppMagn    3.01
	MassSol    11.2
	Orbit
	{
		Period          0.36434795
		SemiMajorAxis   0.0906
		Inclination     92.8
		AscendingNode   302 //-58
		ArgOfPericenter 0
		Epoch           2447025.6
		MeanAnomaly     0
	}
}

Star "ZET Tau B"
{
	ParentBody "ZET Tau"
	Class      "G4 V"
	MassSol    0.94
	Orbit
	{
		Period          0.36434795
		SemiMajorAxis   1.0794
		Inclination     92.8
		AscendingNode   302
		ArgOfPericenter 180
		Epoch           2447025.6
		MeanAnomaly     0
	}
}

//30 Tau;spanish wiki


Star "30 Tau A/HIP 17771/HD 23793"
{
	ParentBody "30 Tau"
	Class      "B3 V"
	Radius     2610000
	AppMagn    5.06
	MassSol    5.5
	Orbit
	{
		Period          24836.27111182
		SemiMajorAxis   308.1401
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "30 Tau B"
{
	ParentBody "30 Tau"
	Class      "F5 V"
	Radius     1183200
	AppMagn    9.41
	MassSol    1.3
	Orbit
	{
		Period          24836.27111182
		SemiMajorAxis   1303.6697
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//CD Tau;spanish wiki

Barycenter "CD Tau A"
{
	ParentBody "CD Tau"
	Orbit
	{
		Period          9605.43
		SemiMajorAxis   143.8694
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "CD Tau Aa/HIP 24663/HD 34335/BD+19 886"
{
	ParentBody "CD Tau A"
	Class      "F5 IV"
	Radius     1252800
	AppMagn    6.77
	MassSol    1.44
	Orbit
	{
		Period          0.00941123
		SemiMajorAxis   0.0307
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "CD Tau Ab"
{
	ParentBody "CD Tau A"
	Class      "F7 V"
	Radius     1099680
	MassSol    1.37
	Orbit
	{
		Period          0.00941123
		SemiMajorAxis   0.0323
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "CD Tau B/HD 34335 B/BD+19 886 B"
{
	ParentBody "CD Tau"
	Class      "K2 V"
	AppMagn    9.88
	MassSol    0.74
	Orbit
	{
		Period          9605.43
		SemiMajorAxis   546.3147
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//EQ Tau;spanish wiki
//contact binary


Star "EQ Tau Aa"
{
	ParentBody "EQ Tau"
	Class      "G1 V"
	Radius     793440
	AppMagn    11.12
	MassSol    1.23
	Orbit
	{
		Period          0.00093507
		SemiMajorAxis   0.0037
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "EQ Tau Ab"
{
	ParentBody "EQ Tau"
	Class      "G1 V"
	Radius     549840
	MassSol    0.54
	Orbit
	{
		Period          0.00093507
		SemiMajorAxis   0.0083
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//RZ Tau;spanish wiki
//A-B contact binary

Barycenter "RZ Tau (AB)"
{
	ParentBody "RZ Tau"
	Orbit
	{
		Period          1461.69
		SemiMajorAxis   13.8544
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "RZ Tau A/HIP 21467/HD 285892"
{
	ParentBody "RZ Tau (AB)"
	Class      "A7 V"
	Radius     1085760
	AppMagn    10.28
	MassSol    1.7
	Orbit
	{
		Period          0.0011389
		SemiMajorAxis   0.004
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "RZ Tau B"
{
	ParentBody "RZ Tau (AB)"
	Class      "A V"
	Radius     723840
	MassSol    0.64
	Orbit
	{
		Period          0.0011389
		SemiMajorAxis   0.0105
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "RZ Tau C"
{
	ParentBody "RZ Tau"
	Class      "M5 V"
	MassSol    0.2
	Orbit
	{
		Period          1461.69
		SemiMajorAxis   162.0965
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//88 Tau
//The Astrophysical Journal, 669:1209–1219, 2007 November 10
//THE ORBITS OF THE QUADRUPLE STAR SYSTEM 88 TAURI A FROM PHASES
//DIFFERENTIAL ASTROMETRY AND RADIAL VELOCITY
//Benjamin F. Lane, Matthew W. Muterspaugh, Francis C. Fekel, Michael Williamson, Stanley Browne
//Maciej Konacki, Bernard F. Burke, M. M. Colavita, S. R. Kulkarni,and Shao
//simbad, only period known for this components


Barycenter "88 Tau A"
{
	ParentBody "88 Tau"
	Orbit
	{
		Period          60725.07
		SemiMajorAxis   1725
		Inclination     69.923      //unknown, RA and IN just aligned
		AscendingNode   146.734
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Barycenter "88 Tau B"
{
	ParentBody "88 Tau"
	Orbit
	{
		Period          60725.07
		SemiMajorAxis   1725
		Inclination     69.923      //unknown, RA and IN just aligned
		AscendingNode   146.734
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}


Barycenter "88 Tau Aa"
{
	ParentBody "88 Tau A"
	Orbit
	{
		Period          18.0411
		SemiMajorAxis   4.60115919
		Eccentricity    0.0715
		Inclination     69.923
		AscendingNode   146.734   
		ArgOfPericenter 205.7
		Epoch           2455261
		MeanAnomaly     0
	}
}

Barycenter "88 Tau (AB)"
{
	ParentBody "88 Tau A"
	Orbit
	{
		Period          18.0411
		SemiMajorAxis   7.40384081
		Eccentricity    0.0715
		Inclination     69.923
		AscendingNode   146.734
		ArgOfPericenter 25.7
		Epoch           2455261
		MeanAnomaly     0
	}
}

Star "88 Tau Aa1/HIP 21402/HD 29140"
{
	ParentBody "88 Tau Aa"
	Class      "A6 V"
	AppMagn    4.44
	MassSol    2.06
	Orbit
	{
		Period          0.0098
		SemiMajorAxis   0.02703302
		Inclination     110.6
		AscendingNode   287.5
		ArgOfPericenter 0
		Epoch           2453389.3824
		MeanAnomaly     0
	}
}

Star "88 Tau Aa2"
{
	ParentBody "88 Tau Aa"
	Class      "F5 V"
	MassSol    1.361
	Orbit
	{
		Period          0.0098
		SemiMajorAxis   0.04091698
		Inclination     110.6
		AscendingNode   287.5
		ArgOfPericenter 180
		Epoch           2453389.3824
		MeanAnomaly     0
	}
}

Star "88 Tau Ab1"
{
	ParentBody "88 Tau (AB)"
	Class      "G2 V"
	AppMagn    6.61
	MassSol    1.069
	Orbit
	{
		Period          0.0216
		SemiMajorAxis   0.04889744
		Inclination     27.23
		AscendingNode   34
		ArgOfPericenter 0
		Epoch           2452507.31
		MeanAnomaly     0
	}
}

Star "88 Tau Ab2"
{
	ParentBody "88 Tau (AB)"
	Class      "G2 V"
	MassSol    1.057
	Orbit
	{
		Period          0.0216
		SemiMajorAxis   0.04945256
		Inclination     27.23
		AscendingNode   34
		ArgOfPericenter 180
		Epoch           2452507.31
		MeanAnomaly     0
	}
}

Star "88 Tau Ba"
{
	ParentBody "88 Tau B"
	Class      "F5 V"
	AppMagn    7.8
	Orbit
	{
		Period          1349
		SemiMajorAxis   2.669
		Inclination     69.923      //unknown, RA and IN just aligned
		AscendingNode   146.734
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "88 Tau Bb"
{
	ParentBody "88 Tau B"
	Class      "F V"
	Orbit
	{
		Period          1349
		SemiMajorAxis   2.669
		Inclination     69.923      //unknown, RA and IN just aligned
		AscendingNode   146.734
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


////////////////END OF TAURUS///////////////////////////

///////////////////////VELA/////////////////////////////

//GAM Vel;spanish, english wiki

Barycenter "Regor A/GAM2 Vel"
{
	ParentBody "Regor"
	Orbit
	{
		Period          264117.291
		SemiMajorAxis   2863.0705
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Regor Aa/HIP 39953/HD 68273"
{
	ParentBody "Regor A"
	Class      "WC8"
	Radius     2018400
	AppMagn    4.27
	MassSol    9
	Orbit
	{
		Period          0.2151
		SemiMajorAxis   0.7692
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Regor Ab"
{
	ParentBody "Regor A"
	Class      "O7 Ib"
	Radius     12528000
	AppMagn    1.78
	MassSol    30
	Orbit
	{
		Period          0.2151
		SemiMajorAxis   0.2308
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Barycenter "Regor B/GAM1 Vel"
{
	ParentBody "Regor"
	Orbit
	{
		Period          264117.291
		SemiMajorAxis   12136.9295
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "Regor Ba"
{
	ParentBody "Regor B"
	Class      "B2 III"
	AppMagn    4.7
	MassSol    4.6
	Orbit
	{
		Period          0.0041
		SemiMajorAxis   0.0265
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Regor Bb"
{
	ParentBody "Regor B"
	Class      "B IV"
	Orbit
	{
		Period          0.0041
		SemiMajorAxis   0.0265
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//DEL Vel;6thCVB,english wiki

Barycenter "DEL Vel A"
{
	ParentBody "DEL Vel"
	Orbit
	{
		Period          146.97
		SemiMajorAxis   11.6009
		Eccentricity    0.482
		Inclination     105.6
		AscendingNode   344.7
		ArgOfPericenter 8.5
		Epoch           2451829.422313
		MeanAnomaly     0
	}
}

Star "DEL Vel Aa/HIP 42913/HD 74956"
{
	ParentBody "DEL Vel A"
	Class      "A0 V"
	Radius     1839528
	AppMagn    2.3
	MassSol    2.53
	Orbit
	{
		Period          0.12369945
		SemiMajorAxis   0.1974
		Eccentricity    0.287
		Inclination     90.96
		AscendingNode   155
		ArgOfPericenter 109.79
		Epoch           2452528.9502
		MeanAnomaly     0
	}
}

Star "DEL Vel Ab"
{
	ParentBody "DEL Vel A"
	Class      "A5 V"
	Radius     1644648
	AppMagn    3.4
	MassSol    2.37
	Orbit
	{
		Period          0.12369945
		SemiMajorAxis   0.2108
		Eccentricity    0.287
		Inclination     90.96
		AscendingNode   155
		ArgOfPericenter 289.79
		Epoch           2452528.9502
		MeanAnomaly     0
	}
}

Star "DEL Vel B"
{
	ParentBody "DEL Vel"
	Class      "F6 V"
	AppMagn    5.5
	MassSol    1.5
	Orbit
	{
		Period          146.97
		SemiMajorAxis   37.8964
		Eccentricity    0.482
		Inclination     105.6
		AscendingNode   344.7
		ArgOfPericenter 188.5
		Epoch           2451829.422313
		MeanAnomaly     0
	}
}

//MU Vel;6thCVB, eng and sp wiki


Star "MU Vel A/HIP 52727/HD 93497"
{
	ParentBody "MU Vel"
	Class      "G5 III"
	Radius     10440000
	AppMagn    2.82
	MassSol    6.2
	Orbit
	{
		Period          138
		SemiMajorAxis   8.305
		Eccentricity    0.84
		Inclination     57
		AscendingNode   59.1
		ArgOfPericenter 178
		Epoch           2433684.189878
		MeanAnomaly     0
	}
}

Star "MU Vel B"
{
	ParentBody "MU Vel"
	Class      "G2 V"
	AppMagn    5.65
	MassSol    1.2
	Orbit
	{
		Period          138
		SemiMajorAxis   42.9094
		Eccentricity    0.84
		Inclination     57
		AscendingNode   59.1
		ArgOfPericenter 358
		Epoch           2433684.189878
		MeanAnomaly     0
	}
}

//PSI Vel;6thCVB, spanish wiki


Star "PSI Vel A/HIP 46651/HD 82434"
{
	ParentBody "PSI Vel"
	Class      "F3 IV"
	Radius     1113600
	AppMagn    3.91
	MassSol    1.5
	Orbit
	{
		Period          33.95
		SemiMajorAxis   7.9986
		Eccentricity    0.433
		Inclination     58
		AscendingNode   291
		ArgOfPericenter 44.3
		Epoch           2440470.389931
		MeanAnomaly     0
	}
}

Star "PSI Vel B"
{
	ParentBody "PSI Vel"
	Class      "F0 IV"
	Radius     835200
	AppMagn    5.12
	MassSol    1.5
	Orbit
	{
		Period          33.95
		SemiMajorAxis   7.9986
		Eccentricity    0.433
		Inclination     58
		AscendingNode   291
		ArgOfPericenter 224.3
		Epoch           2440470.389931
		MeanAnomaly     0
	}
}

//CV Vel;spanish wiki

Star "CV Vel A/HIP 44245/HD 77464"
{
	ParentBody "CV Vel"
	Class      "B2 V"
	Radius     2874480
	AppMagn    6.69
	MassSol    6.07
	Orbit
	{
		Period          0.01887397
		SemiMajorAxis   0.0793
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "CV Vel B"
{
	ParentBody "CV Vel"
	Class      "B2 V"
	Radius     2721360 
	MassSol    5.97
	Orbit
	{
		Period          0.01887397
		SemiMajorAxis   0.0807
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


//KX Vel;spanish wiki


Barycenter "KX Vel A"
{
	ParentBody "KX Vel"
	Orbit
	{
		Period          31633.21
		SemiMajorAxis   345.0465
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "KX Vel Aa/HIP 43413/HD 75821"
{
	ParentBody "KX Vel A"
	Class      "B0 III" 
	AppMagn    5.09
	MassSol    13.8
	Orbit
	{
		Period          0.07208219
		SemiMajorAxis   0.262
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "KX Vel Ab"
{
	ParentBody "KX Vel A"
	AppMagn    10 //unknown
	Orbit
	{
		Period          0.07208219
		SemiMajorAxis   0.262
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "KX Vel B"
{
	ParentBody "KX Vel"
	Class      "B V" //unknown; related with the absmag
	AppMagn    10
	Orbit
	{
		Period          31633.21
		SemiMajorAxis   2800.9658
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


//LU Vel;spanish wiki


Star "LU Vel A/HIP 48904"
{
	ParentBody "LU Vel"
	Class      "M3 V"
	Radius     278400
	AppMagn    11.27
	MassSol    0.36
	Orbit
	{
		Period          0.00514137
		SemiMajorAxis   0.0128
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "LU Vel B"
{
	ParentBody "LU Vel"
	Class      "M3 V"
	MassSol    0.35
	Orbit
	{
		Period          0.00514137
		SemiMajorAxis   0.0132
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


//p Vel;6thCVB,spanish wiki


Barycenter "p Vel A"
{
	ParentBody "p Vel"
	Orbit
	{
		Period          16.54
		SemiMajorAxis   3.7487
		Eccentricity    0.746
		Inclination     129.1
		AscendingNode   28.2
		ArgOfPericenter 283.2
		Epoch           2452537.992179
		MeanAnomaly     0
	}
}

Star "p Vel Aa/HIP 51986/HD 92139"
{
	ParentBody "p Vel A"
	Class      "F3 IV"
	AppMagn    4.2
	MassSol    2.13
	Orbit
	{
		Period          0.0279726
		SemiMajorAxis   0.0669
		Inclination     129.1  //unknown RA and IN, just aligned
		AscendingNode   28.2
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "p Vel Ab"
{
	ParentBody "p Vel A"
	Class      "F0 V"
	MassSol    1.81
	Orbit
	{
		Period          0.0279726
		SemiMajorAxis   0.0788
		Inclination     129.1   //unknown RA and IN, just aligned
		AscendingNode   28.2
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "p Vel B"
{
	ParentBody "p Vel"
	Class      "A6 V"
	AppMagn    5.1
	MassSol    2.41
	Orbit
	{
		Period          16.54
		SemiMajorAxis   6.1286
		Eccentricity    0.746
		Inclination     129.1
		AscendingNode   28.2
		ArgOfPericenter 103.2
		Epoch           2452537.992179
		MeanAnomaly     0
	}
}

//PT Vel; spanish wiki


Star "PT Vel A/HIP 45079/HD 79154"
{
	ParentBody "PT Vel"
	Class      "A1 V"
	Radius     1454640
	AppMagn    7.05
	MassSol    2.22
	Orbit
	{
		Period          0.00493699
		SemiMajorAxis   0.0191
		Eccentricity    0.13
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "PT Vel B"
{
	ParentBody "PT Vel"
	Class      "A6 V"
	Radius     1099680
	MassSol    1.63
	Orbit
	{
		Period          0.00493699
		SemiMajorAxis   0.0259
		Eccentricity    0.13
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//S Vel; spanish wiki
//close binary


Star "S Vel A/HIP 46881/HD 82829"
{
	ParentBody "S Vel"
	Class      "A5 V"
	Radius     1552080
	AppMagn    7.81
	MassSol    2.55
	Orbit
	{
		Period          0.01625644
		SemiMajorAxis   0.0139
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "S Vel B"
{
	ParentBody "S Vel"
	Class      "K4 IV"
	Radius     3222480
	MassSol    0.45
	Orbit
	{
		Period          0.01625644
		SemiMajorAxis   0.0786
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


//V390 Vel;spanish wiki


Star "V390 Vel A"
{
	ParentBody "V390 Vel"
	Class      "F3 III" //F3e, post AGB dying star
	AppMagn    9.2
	MassSol    1
	Orbit
	{
		Period          1.36712329
		SemiMajorAxis   0.3935
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "V390 Vel B"
{
	ParentBody "V390 Vel"
	Class      "M V" //unknown, could be also a white dwarf
	MassSol    0.4
	Orbit
	{
		Period          1.36712329
		SemiMajorAxis   0.9837
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


///////////////////////VIRGO///////////////////////////

//Spica;english wiki

Star "Spica A/HIP 65474/HD 116658"
{
	ParentBody "Spica"
	Class      "B1 IV"
	Radius     5150400
	AppMagn    1.04
	MassSol    10.25
	Orbit
	{
		Period          0.01135862
		SemiMajorAxis   0.0517
		Eccentricity    0.067
		Inclination     54
		ArgOfPericenter 140
		Epoch           2440678.09
		MeanAnomaly     0
	}
}

Star "Spica B"
{
	ParentBody "Spica"
	Class      "B2 V"
	Radius     2533440
	MassSol    6.97
	Orbit
	{
		Period          0.01135862
		SemiMajorAxis   0.076
		Eccentricity    0.067
		Inclination     54
		ArgOfPericenter 320
		Epoch           2440678.09
		MeanAnomaly     0
	}
}

//ETA Vir;6thCVB, english and spanish wiki



Barycenter "ETA Vir A"
{
	ParentBody "ETA Vir"
	Orbit
	{
		Period          13.1
		SemiMajorAxis   3.0087
		Eccentricity    0.08
		Inclination     50
		AscendingNode   173
		ArgOfPericenter 4
		Epoch           2447965.15985
		MeanAnomaly     0
	}
}

Star "ETA Vir Aa/HIP 60129/HD 107259"
{
	ParentBody "ETA Vir A"
	Class      "A2 V"
	AppMagn    3.89
	MassSol    2.5
	Orbit
	{
		Period          0.19669014
		SemiMajorAxis   0.2386
		Eccentricity    0.2519
		Inclination     45.5
		AscendingNode   173 //unknown AN, just aligned
		ArgOfPericenter 197.21
		Epoch           2454403.6116
		MeanAnomaly     0
	}
}

Star "ETA Vir Ab"
{
	ParentBody "ETA Vir A"
	Class      "A V"
	MassSol    1.89
	Orbit
	{
		Period          0.19669014
		SemiMajorAxis   0.316
		Eccentricity    0.2519
		Inclination     45.5
		AscendingNode   173 //unknown AN, just aligned
		ArgOfPericenter 17.21
		Epoch           2454403.6116
		MeanAnomaly     0
	}
}

Star "ETA Vir B"
{
	ParentBody "ETA Vir"
	Class      "A V"
	AppMagn    5.9
	MassSol    1.66
	Orbit
	{
		Period          13.1
		SemiMajorAxis   7.9652
		Eccentricity    0.08
		Inclination     50
		AscendingNode   173
		ArgOfPericenter 184
		Epoch           2447965.15985
		MeanAnomaly     0
	}
}


//ZET Vir;english, spanish wiki

Star "ZET Vir A/HIP 66249/HD 118098"
{
	ParentBody "ZET Vir"
	Class      "A3 V"
	Radius     1446984
	AppMagn    3.376
	MassSol    2.41
	Orbit
	{
		Period          124
		SemiMajorAxis   1.6227
		Eccentricity    0.16
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "ZET Vir B"
{
	ParentBody "ZET Vir"
	Class      "M V"
	MassSol    0.168
	Orbit
	{
		Period          124
		SemiMajorAxis   23.2773
		Eccentricity    0.16
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//LAM Vir;6thCVB, spanish wiki


Star "LAM Vir A/HIP 69974/HD 125337"
{
	ParentBody "LAM Vir"
	Class      "A1 V"
	Radius     1635600
	AppMagn    5
	MassSol    1.9
	Orbit
	{
		Period          0.5664
		SemiMajorAxis   0.5498
		Eccentricity    0.061
		Inclination     109.86
		AscendingNode   196.4
		ArgOfPericenter 272.28
		Epoch           2453070.3
		MeanAnomaly     0
	}
}

Star "LAM Vir B"
{
	ParentBody "LAM Vir"
	Class      "A V"
	Radius     1280640
	AppMagn    5.63
	MassSol    1.79
	Orbit
	{
		Period          0.5664
		SemiMajorAxis   0.5836
		Eccentricity    0.061
		Inclination     109.86
		AscendingNode   196.4
		ArgOfPericenter 92.28
		Epoch           2453070.3
		MeanAnomaly     0
	}
}


//Porrima;6thCVB, english and spanish wiki


Star "Porrima A/HIP 61941/HD 110379"
{
	ParentBody "Porrima"
	Class      "F0 V"
	Radius     835200
	AppMagn    3.48
	MassSol    1.5
	Orbit
	{
		Period          169.104
		SemiMajorAxis   21.2647
		Eccentricity    0.8815
		Inclination     149.46
		AscendingNode   35.34
		ArgOfPericenter 255.02
		Epoch           2453557.383156
		MeanAnomaly     0
	}
}

Star "Porrima B"
{
	ParentBody "Porrima"
	Class      "F0 V"
	Radius     835200
	AppMagn    3.53
	MassSol    1.5
	Orbit
	{
		Period          169.104
		SemiMajorAxis   21.2647
		Eccentricity    0.8815
		Inclination     149.46
		AscendingNode   35.34
		ArgOfPericenter 75.02
		Epoch           2453557.383156
		MeanAnomaly     0
	}
}

//TAU Vir;spanish wiki


Star "TAU Vir A/HIP 68520/HD 122408"
{
	ParentBody "TAU Vir"
	Class      "A3 V"
	Radius     2923200
	AppMagn    4.24
	MassSol    2.5
	Orbit
	{
		Period          17394.80596095
		SemiMajorAxis   229.3535
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "TAU Vir B"
{
	ParentBody "TAU Vir"
	Class      "K V" //unknown, related with absmag
	AppMagn    11.94
	Orbit
	{
		Period          17394.80596095
		SemiMajorAxis   764.5116
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


//TET Vir;spanish wiki


Barycenter "TET Vir (ABC)"
{
	ParentBody "TET Vir"
	Orbit
	{
		Period          230000
		SemiMajorAxis   1027.0943
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Barycenter "TET Vir A"
{
	ParentBody "TET Vir (ABC)"
	Orbit
	{
		Period          7800
		SemiMajorAxis   551.7463
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}



Star "TET Vir Aa/HIP 64238/HD 114330"
{
	ParentBody "TET Vir A"
	Class      "A1 V"
	Radius     2992800
	AppMagn    4.38
	MassSol    2.5
	Orbit
	{
		Period          0.3178
		SemiMajorAxis   16.5862
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "TET Vir Ab"
{
	ParentBody "TET Vir A"
	Class      "A5 V"
	Radius     835200
	MassSol    1.85
	Orbit
	{
		Period          0.3178
		SemiMajorAxis   22.4138
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "TET Vir B"
{
	ParentBody "TET Vir (ABC)"
	Class      "G0 V"
	AppMagn    9.4
	Orbit
	{
		Period          7800
		SemiMajorAxis   138.2537
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "TET Vir C"
{
	ParentBody "TET Vir"
	Class      "G8 V"
	AppMagn    10.4
	Orbit
	{
		Period          230000
		SemiMajorAxis   5944.0351
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

//17 Vir;spanish wiki


Star "17 Vir A/HIP 60353/HD 107705"
{
	ParentBody "17 Vir"
	Class      "F8 V"
	Radius     835200
	AppMagn    6.46
	MassSol    1.22
	Orbit
	{
		Period          15384.71992796
		SemiMajorAxis   295.0508
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "17 Vir B"
{
	ParentBody "17 Vir"
	Class      "K5 V"
	AppMagn    9.2
	MassSol    0.75
	Orbit
	{
		Period          15384.71992796
		SemiMajorAxis   479.9492
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//BH Vir; spanish wiki


Star "BH Vir A/HIP 68258/HD 121909"
{
	ParentBody "BH Vir"
	Class      "F8 V"
	Radius     870000
	AbsMagn    4.05
	MassSol    1.17
	Orbit
	{
		Period          0.00223808
		SemiMajorAxis   0.01055094
		ArgOfPericenter 0
 
		MeanAnomaly     0
	}
}

Star "BH Vir B"
{
	ParentBody "BH Vir"
	Class      "G5 V"
	AbsMagn    4.81
	Radius     793440
	MassSol    1.05
	Orbit
	{
		Period          0.00223808
		SemiMajorAxis   0.01175676
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//GR Vir; spanish wiki
//contact binary


Star "GR Vir A/HIP 72138/HD 129903"
{
	ParentBody "GR Vir"
	Class      "G0 V"
	Radius     988320
	AppMagn    7.96
	MassSol    1.36
	Orbit
	{
		Period          0.00115014
		SemiMajorAxis   0.00140467
		ArgOfPericenter 0
 
		MeanAnomaly     0
	}
}

Star "GR Vir B"
{
	ParentBody "GR Vir"
	Class      "G0 V"
	Radius     424560
	MassSol    0.17
	Orbit
	{
		Period          0.00115014
		SemiMajorAxis   0.01123736
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//GJ 454;english,spanish wiki


Star "GJ 454 A/HIP 58576/HD 104304"
{
	ParentBody "GJ 454"
	Class      "G8 IV"
	AppMagn    5.54
	MassSol    1.01
	Orbit
	{
		Period          48.5
		SemiMajorAxis   2.40983607
		Eccentricity    0.29
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "GJ 454 B"
{
	ParentBody "GJ 454"
	Class      "M4 V"
	MassSol    0.21
	Orbit
	{
		Period          48.5
		SemiMajorAxis   11.59016393
		Eccentricity    0.29
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//HT Vir;6thCVB,spanish wiki


Barycenter "HT Vir B"
{
	ParentBody "HT Vir"
	Orbit
	{
		Period          261.6
		SemiMajorAxis   30.9753
		Eccentricity    0.638
		Inclination     42.1
		AscendingNode   0.5
		ArgOfPericenter 68.9
		Epoch           2442753.153673
		MeanAnomaly     0
	}
}

Barycenter "HT Vir A"
{
	ParentBody "HT Vir"
	Orbit
	{
		Period          261.6
		SemiMajorAxis   33.9253
		Eccentricity    0.638
		Inclination     42.1
		AscendingNode   0.5
		ArgOfPericenter 248.9
		Epoch           2442753.153673
		MeanAnomaly     0
	}
}

Star "HT Vir Aa"
{
	ParentBody "HT Vir A"
	Class      "F V"
	AppMagn    8.1
	MassSol    1.05    //unknown Mass distribution Ab Aa
	Orbit
	{
		Period          0.0889
		SemiMajorAxis   0.1277
		Inclination     42.1    //unknown, AN and IN just aligned
		AscendingNode   0.5
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "HT Vir Ab"
{
	ParentBody "HT Vir A"
	Class      "F V"
	MassSol    1.05    //unknown Mass distribution Ab Aa
	Orbit
	{
		Period          0.0889
		SemiMajorAxis   0.1277
		Inclination     42.1   //unknown, AN and IN just aligned
		AscendingNode   0.5
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "HT Vir Ba/HIP 67186/HD 119931"
{
	ParentBody "HT Vir B"
	Class      "F8 V"
	AppMagn    7.89
	MassSol    1.27
	Orbit
	{
		Period          0.0011
		SemiMajorAxis   0.0064
		Inclination     42.1    //unknown, AN and IN just aligned
		AscendingNode   0.5
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "HT Vir Bb"
{
	ParentBody "HT Vir B"
	Class      "F V"
	MassSol    1.03
	Orbit
	{
		Period          0.0011
		SemiMajorAxis   0.0079
		Inclination     42.1    //unknown, AN and IN just aligned
		AscendingNode   0.5
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//6thCVB;english wiki

Star "PI Vir A/HIP 58590/HD 104321"
{
	ParentBody "PI Vir"
	Class      "A5 V"
	AppMagn    5
	Orbit
	{
		Period          0.774
		SemiMajorAxis   0.1436
		Eccentricity    0.265
		Inclination     62.71
		AscendingNode   149.34
		ArgOfPericenter 312
		Epoch           2448281.3906
		MeanAnomaly     0
	}
}

Star "PI Vir B"
{
	ParentBody "PI Vir"
	Class      "A V"  //unknown,related with appmag
	AppMagn    7
	Orbit
	{
		Period          0.774
		SemiMajorAxis   0.2441
		Eccentricity    0.265
		Inclination     62.71
		AscendingNode   149.34
		ArgOfPericenter 132
		Epoch           2448281.3906
		MeanAnomaly     0
	}
}

Star	"Wolf 424 A/Gliese 473 A/LHS 333 A"
{
	ParentBody	   "Wolf 424"
	Class		   "M6V"
	MassSol			0.143
	AppMagn			13.22
	Orbit
	{
		SemiMajorAxis	1.942
		Period			15.532
		Eccentricity	0.295
		Inclination		103
		AscendingNode	143.48
		ArgOfPericenter	347.2
		MeanAnomaly		0
		Epoch			1992.297
	}
}

Star	"Wolf 424 B/Gliese 473 B/LHS 333 B/FL Vir"
{
	ParentBody	   "Wolf 424"
	Class		   "M6V"
	MassSol			0.131
	AppMagn			13.21
	Orbit
	{
		SemiMajorAxis	2.12
		Period			15.532
		Eccentricity	0.295
		Inclination		103
		AscendingNode	143.48
		ArgOfPericenter	167.2
		MeanAnomaly		0
		Epoch			1992.297
	}
}

///////////////////////LIBRA//////////////////////////////////

//Zubenelgenubi

//A&A 514, A98 (2010)
//DOI: 10.1051/0004-6361/200913986 ESO 2010
//Reaching the boundary between stellar kinematic groups
//and very wide binaries
//J. A. Caballero

//SIMBAD,english wiki


Barycenter "ALF1 Lib/8 Lib"
{
	ParentBody "Zubenelgenubi"
	Orbit
	{
		Period          292784.82
		SemiMajorAxis   3613.0198915
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "ALF1 Lib A/HIP 72603/HD 130819"
{
	ParentBody "ALF1 Lib"
	Class      "F3 V"
	AppMagn    5.153
	MassSol    1.33
	Orbit
	{
		Period          16.0822
		SemiMajorAxis   2.73224044
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "ALF1 Lib B"
{
	ParentBody "ALF1 Lib"
	Class      "M V"
	MassSol    0.5
	Orbit
	{
		Period          16.0822
		SemiMajorAxis   7.26775956
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Barycenter "ALF2 Lib/9 Lib"
{
	ParentBody "Zubenelgenubi"
	Orbit
	{
		Period          292784.82
		SemiMajorAxis   1786.9801085
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "ALF2 Lib A/HIP 72622/HD 130841"
{
	ParentBody "ALF2 Lib"
	Class      "A4 IV"
	AppMagn    2.741
	MassSol    2.2
	Orbit
	{
		Period          0.1644
		SemiMajorAxis   0.21885114
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "ALF2 Lib B"
{
	ParentBody "ALF2 Lib"
	Class      "A5 V"
	MassSol    1.5
	Orbit
	{
		Period          0.1644
		SemiMajorAxis   0.32098167
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}



//EPS Lib;6thCVB, spanish wiki

Star "EPS Lib A/HIP 75379/HD 137052"
{
	ParentBody "EPS Lib"
	Class      "F4 V"
	AppMagn    4.93
	MassSol    1.6
	Orbit
	{
		Period          0.6216
		SemiMajorAxis   0.1366
		Eccentricity    0.68
		Inclination     52.6
		AscendingNode   215.5
		ArgOfPericenter 339.5
		Epoch           2414785.1
		MeanAnomaly     0
	}
}

Star "EPS Lib B"
{
	ParentBody "EPS Lib"
	AppMagn    10 //unknown
	Orbit
	{
		Period          0.6216
		SemiMajorAxis   0.1366
		Eccentricity    0.68
		Inclination     52.6
		AscendingNode   215.5
		ArgOfPericenter 159.5
		Epoch           2414785.1
		MeanAnomaly     0
	}
}

//HD 134439;spanish wiki


Star "GJ 9511 A/HIP 74235/HD 134439"
{
	ParentBody "GJ 9511"
	Class      "K2 V"
	Radius     396720
	AppMagn    9.07
	MassSol    0.56
	Orbit
	{
		Period          1183781.37719412
		SemiMajorAxis   5737.83783784
		ArgOfPericenter 0
 
		MeanAnomaly     0
	}
}

Star "GJ 9511 B/HD 134440"
{
	ParentBody "GJ 9511"
	Class      "K2 V"
	Radius     375840
	AppMagn    9.43
	MassSol    0.55
	Orbit
	{
		Period          1183781.37719412
		SemiMajorAxis   5842.16216216
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//IOT1 Lib;6thCVB,spanish wiki


Barycenter "IOT1 Lib A"
{
	ParentBody "IOT1 Lib"
	Orbit
	{
		Period          192865.2593
		SemiMajorAxis   1457.14285714
		Inclination     154.2    //unknown IN and AN, just aligned
		AscendingNode   174.5
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Barycenter "IOT1 Lib BC"
{
	ParentBody "IOT1 Lib"
	Orbit
	{
		Period          192865.2593
		SemiMajorAxis   5142.85714286
		Inclination     154.2   //unknown IN and AN, just aligned
		AscendingNode   174.5
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}



Star "IOT1 Lib Aa/HIP 74392/HD 134759"
{
	ParentBody "IOT1 Lib A"
	Class      "B9 V"
	AppMagn    4.54
	MassSol    3.1
	Orbit
	{
		Period          23.46
		SemiMajorAxis   7.20166667
		Eccentricity    0.244
		Inclination     154.2
		AscendingNode   174.5
		ArgOfPericenter 341.5
		Epoch           2440999.991119
		MeanAnomaly     0
	}
}

Star "IOT1 Lib Ab"
{
	ParentBody "IOT1 Lib A"
	Class      "B9 V"
	MassSol    2.9
	Orbit
	{
		Period          23.46
		SemiMajorAxis   7.69833333
		Eccentricity    0.244
		Inclination     154.2
		AscendingNode   174.5
		ArgOfPericenter 161.5
		Epoch           2440999.991119
		MeanAnomaly     0
	}
}

Star "IOT1 Lib B"
{
	ParentBody "IOT1 Lib BC"
	Class      "G4 V"
	AppMagn    10
	MassSol    0.85  //unknown Mass distribution BC
	Orbit
	{
		Period          2700
		SemiMajorAxis   115
		Inclination     154.2   //unknown IN and AN, just aligned
		AscendingNode   174.5
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "IOT1 Lib C"
{
	ParentBody "IOT1 Lib BC"
	Class      "G8 V"
	AppMagn    11
	MassSol    0.85
	Orbit
	{
		Period          2700
		SemiMajorAxis   115
		Inclination     154.2   //unknown IN and AN, just aligned
		AscendingNode   174.5
		ArgOfPericenter 161.5
		MeanAnomaly     0
	}
}

//LAM Lib;spanish wiki

Star "LAM Lib A/HIP 77811/HD 142096"
{
	ParentBody "LAM Lib"
	Class      "B3 V"
	Radius     2714400
	AppMagn    5.03
	MassSol    6.3
	Orbit
	{
		Period          0.03419178
		SemiMajorAxis   0.12249769
		Eccentricity    0.27
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "LAM Lib B"
{
	ParentBody "LAM Lib"
	Class      "B V"
	Orbit
	{
		Period          0.03419178
		SemiMajorAxis   0.12249769
		Eccentricity    0.27
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//MU Lib;spanish wiki


Barycenter "MU Lib (AB)"
{
	ParentBody "MU Lib"
	Orbit
	{
		Period          36000
		SemiMajorAxis   202.5197
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "MU Lib A/HIP 72489/HD 130559"
{
	ParentBody "MU Lib (AB)"
	Class      "A1 V"
	Radius     1802640
	AppMagn    5.69
	MassSol    2.1
	Orbit
	{
		Period          704
		SemiMajorAxis   72.6591
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "MU Lib B"
{
	ParentBody "MU Lib (AB)"
	Class      "A6 V"
	AppMagn    6.72
	MassSol    2.3
	Orbit
	{
		Period          704
		SemiMajorAxis   66.3409
		ArgOfPericenter 180
 
		MeanAnomaly     0
	}
}

Star "MU Lib C"
{
	ParentBody "MU Lib"
	Class      "M V"
	AbsMagn    12.5
	MassSol    0.5
	Orbit
	{
		Period          36000
		SemiMajorAxis   1782.1735
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


//TAU Lib;spanish wiki


Star "TAU Lib A/HIP 76600/HD 139365"
{
	ParentBody "TAU Lib"
	Class      "B2 V"
	Radius     3584400
	AppMagn    3.65
	MassSol    6.8
	Orbit
	{
		Period          0.0090411
		SemiMajorAxis   5.20971908
		Eccentricity    0.3
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "TAU Lib B"
{
	ParentBody "TAU Lib"
	Class      "B5 V"
	MassSol    4.6
	Orbit
	{
		Period          0.0090411
		SemiMajorAxis   7.70132386
		Eccentricity    0.3
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


//UPS Lib;spanish wiki
//A component with unknown Mass
//supposed 2 MS for     Orbit distribution



Star "UPS Lib A/HIP 76470/HD 139063"
{
	ParentBody "UPS Lib"
	Class      "K5 III"
	Radius     21576000
	AppMagn    3.61
	Orbit
	{
		Period          5.64582587
		SemiMajorAxis   61.8404908
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "UPS Lib B"
{
	ParentBody "UPS Lib"
	Class      "K V" //unknown,related with absmag
	AppMagn    10.8
	Orbit
	{
		Period          5.64582587
		SemiMajorAxis   164.90797546
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//VZ Lib;spanish wiki

Barycenter "VZ Lib A"
{
	ParentBody "VZ Lib"
	Orbit
	{
		Period          36000
		SemiMajorAxis   62.9895
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "VZ Lib Aa"
{
	ParentBody "VZ Lib A"
	Class      "F5 V"
	Radius     814320
	AppMagn    10.34
	MassSol    1.06
	Orbit
	{
		Period          0.00098137
		SemiMajorAxis   0.003
		ArgOfPericenter 0
 
		MeanAnomaly     0
	}
}

Star "VZ Lib Ab"
{
	ParentBody "VZ Lib A"
	Class      "F V"
	Radius     501120
	MassSol    0.35
	Orbit
	{
		Period          0.00098137
		SemiMajorAxis   0.0091
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "VZ Lib B"
{
	ParentBody "VZ Lib"
	Class      "G7 V"
	MassSol    0.9
	Orbit
	{
		Period          36000
		SemiMajorAxis   98.6835
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


/////////////////AQUARIUS////////////////////////////////////

Barycenter	"EZ Aqr (AC)"
{
	ParentBody	   "EZ Aqr"
	Orbit
	{
		SemiMajorAxis	0.123
		Period			2.2506
		Eccentricity	0.437
		Inclination		112.4
		AscendingNode	162.1
		ArgOfPericenter	-17.7
		MeanAnomaly		0
		Epoch			1987.236
	}
}

Star	"EZ Aqr A/GJ 866 A"
{
	ParentBody	   "EZ Aqr (AC)"
	Class		   "M5 V"
	AppMagn			13.33
	MassSol			0.11
	Radius			347750
	Orbit
	{
		SemiMajorAxis	0.0143
		Period			0.01040405546
		Inclination		110
		AscendingNode	160 // from (AC)-B pair
		ArgOfPericenter	0
		MeanAnomaly		0
	}
}

Star	"EZ Aqr C/GJ 866 C"
{
	ParentBody	   "EZ Aqr (AC)"
	Class		   "M5 V"
	AppMagn			14.03
	MassSol			0.1
	Radius			347750
	Orbit
	{
		SemiMajorAxis	0.0157
		Period			0.01040405546
		Inclination		110
		AscendingNode	160 // from (AC)-B pair
		ArgOfPericenter	180
		MeanAnomaly		0
	}
}

Star	"EZ Aqr B/GJ 866 B"
{
	ParentBody	   "EZ Aqr"
	Class		   "M5 V"
	AppMagn			13.27
	MassSol			0.11
	Radius			347750
	Orbit
	{
		SemiMajorAxis	0.234
		Period			2.2506
		Eccentricity	0.437
		Inclination		112.4
		AscendingNode	162.1
		ArgOfPericenter	162.3
		MeanAnomaly		0
		Epoch			1987.236
	}
}

//53 Aqr;6thCVB, english and spanish wiki

Star "53 Aqr A/HIP 110778/HD 212697"
{
	ParentBody "53 Aqr"
	Class      "G1 V"
	Radius     772560
	AppMagn    6.29
	MassSol    0.99
	Orbit
	{
		Period          3500
		SemiMajorAxis   150.6258
		Eccentricity    0.9
		Inclination     44.13
		AscendingNode   294.55
		ArgOfPericenter 151.4
		Epoch           2459945.10397
		MeanAnomaly     0
	}
}

Star "53 Aqr B"
{
	ParentBody "53 Aqr"
	Class      "G5 V"
	AppMagn    6.39
	Orbit
	{
		Period          3500
		SemiMajorAxis   150.6258
		Eccentricity    0.9
		Inclination     44.13
		AscendingNode   294.55
		ArgOfPericenter 331.4
		Epoch           2459945.10397
		MeanAnomaly     0
	}
}


//GAM Aqr;spanish,english wiki


Star "Sadachbia A/GAM Aqr A/HIP 110395/HD 212061"
{
	ParentBody "Sadachbia"
	Class      "A0 V"
	Radius     2088000
	AppMagn    3.849
	Orbit
	{
		Period          0.15890411
		SemiMajorAxis   0.032
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Sadachbia B/GAM Aqr B"
{
	ParentBody "Sadachbia"
	Class      "M V" //unknown, low Mass companion acording 3rd kepler law
	Orbit
	{
		Period          0.15890411
		SemiMajorAxis   0.368
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//OME2 Aqr;english and spanish wiki


Star "OME2 Aqr A/HIP 116971/HD 222661"
{
	ParentBody "OME2 Aqr"
	Class      "B9 V"
	Radius     1531200
	AppMagn    4.48
	Orbit
	{
		Period          2349
		SemiMajorAxis   60.8695029
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "OME2 Aqr B"
{
	ParentBody "OME2 Aqr"
	Class      "K V"
	AppMagn    9.5
	Orbit
	{
		Period          2349
		SemiMajorAxis   199.6519695
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


//PI Aqr;spanish, english wiki


Star "Seat A/PI Aqr A/HIP 110672/HD 212571"
{
	ParentBody "Seat"
	Class      "B1 V"
	Radius     4315200
	AppMagn    4.66
	MassSol    10.7
	Orbit
	{
		Period          0.23041096
		SemiMajorAxis   0.17045455
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Seat B"
{
	ParentBody "Seat"
	Class      "B V"
	MassSol    2.5
	Orbit
	{
		Period          0.23041096
		SemiMajorAxis   0.72954545
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//PSI1 Aqr;with exoplanet

//PSI3 Aqr;english wiki


Star "PSI3 Aqr A/HIP 115115/HD 219832"
{
	ParentBody "PSI3 Aqr"
	Class      "A0 V"
	Radius     1392000
	AppMagn    4.98
	Orbit
	{
		Period          746.41
		SemiMajorAxis   32.53
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "PSI3 Aqr B"
{
	ParentBody "PSI3 Aqr"
	Class      "K V" //unknown;related with appmag
	AppMagn    11
	Orbit
	{
		Period          746.41
		SemiMajorAxis   88.02
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


//KSI Aqr;6thCVB, english wiki


Star "KSI Aqr A/HIP 106786/HD 205767"
{
	ParentBody "KSI Aqr"
	Class      "A7 V"
	AppMagn    4.7
	MassSol    1.9
	Orbit
	{
		Period          25.5
		SemiMajorAxis   4.9417
		Eccentricity    0.6
		Inclination     95
		AscendingNode   300
		ArgOfPericenter 270
		Epoch           2440733.364314
		MeanAnomaly     0
	}
}

Star "KSI Aqr B"
{
	ParentBody "KSI Aqr"
	Class      "M V" //unknown, could be also a white dwarf
	MassSol    0.9
	Orbit
	{
		Period          25.5
		SemiMajorAxis   10.4325
		Eccentricity    0.6
		Inclination     95
		AscendingNode   300
		ArgOfPericenter 90
		Epoch           2440733.364314
		MeanAnomaly     0
	}
}


//ZET Aqr;6thCVB,english and spanish wiki


Barycenter "ZET2 Aqr"
{
	ParentBody "ZET Aqr"
	Orbit
	{
		Period          486.7
		SemiMajorAxis   51.868
		Eccentricity    0.343
		Inclination     141.7
		AscendingNode   133.2
		ArgOfPericenter 273
		Epoch           2445236.800625
		MeanAnomaly     0
	}
}

Star "ZET1 Aqr/HD 213052/HIP 110960"
{
	ParentBody "ZET Aqr"
	Class      "F3 V"
	AppMagn    4.36
	MassSol    1.72
	Orbit
	{
		Period          486.7
		SemiMajorAxis   43.5185
		Eccentricity    0.343
		Inclination     141.7
		AscendingNode   133.2
		ArgOfPericenter 93
		Epoch           2445236.800625
		MeanAnomaly     0
	}
}

Star "ZET2 Aqr A/HD 213051"
{
	ParentBody "ZET2 Aqr"
	Class      "F6 IV"
	AppMagn    4.57
	MassSol    1.65
	Orbit
	{
		Period          25.822
		SemiMajorAxis   2.1073
		Eccentricity    0.125
		Inclination     141.7
		AscendingNode   133.2
		ArgOfPericenter 330.3
		Epoch           2452787.817843
		MeanAnomaly     0
	}
}

Star "ZET2 Aqr B"
{
	ParentBody "ZET2 Aqr"
	Class      "M0 V" //unconfirmed
	MassSol    0.4
	Orbit
	{
		Period          25.822
		SemiMajorAxis   8.6927
		Eccentricity    0.125
		Inclination     141.7
		AscendingNode   133.2
		ArgOfPericenter 150.3
		Epoch           2452787.817843
		MeanAnomaly     0
	}
}

//94 Aqr;6thCV,english and spanish wiki


Barycenter "94 Aqr A"
{
	ParentBody "94 Aqr"
	Orbit
	{
		Period          2554.65
		SemiMajorAxis   83.0652
		Inclination     45.8
		AscendingNode   162.6
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "94 Aqr Aa/HIP 115126/HD 219834"
{
	ParentBody "94 Aqr A"
	Class      "G8 IV"
	Radius     1322400
	AppMagn    5.19
	MassSol    1.29
	Orbit
	{
		Period          6.314
		SemiMajorAxis   1.6581
		Eccentricity    0.191
		Inclination     45.8
		AscendingNode   162.6
		ArgOfPericenter 208.3
		Epoch           2444506.316228
		MeanAnomaly     0
	}
}

Star "94 Aqr Ab"
{
	ParentBody "94 Aqr A"
	Class      "K V"
	Radius     647280
	AppMagn    6.7
	MassSol    0.93
	Orbit
	{
		Period          6.314
		SemiMajorAxis   2.2999
		Eccentricity    0.191
		Inclination     45.8
		AscendingNode   162.6
		ArgOfPericenter 28.3
		Epoch           2444506.316228
		MeanAnomaly     0
	}
}

Star "94 Aqr B"
{
	ParentBody "94 Aqr"
	Class      "K2 V"
	AppMagn    7.52
	MassSol    0.96
	Orbit
	{
		Period          2554.65
		SemiMajorAxis   192.0882
		Inclination     45.8    //unknown,just aligned
		AscendingNode   162.6
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//AE Aqr;WD STAR PRESENT

//BD-22 5866

//BD –22°5866: A Low-Mass Quadruple-lined 
//Spectroscopic and Eclipsing Binary
//Evgenya Shkolnik

//spanish wiki

Barycenter "BD-22 5866 A"
{
	ParentBody "BD-22 5866"
	Orbit
	{
		Period          10.326
		SemiMajorAxis   2.6934764
		Inclination     85.5
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Barycenter "BD-22 5866 B"
{
	ParentBody "BD-22 5866"
	Orbit
	{
		Period          10.326
		SemiMajorAxis   3.4065236
		Inclination     85.5
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}



Star "BD-22 5866 Aa"
{
	ParentBody "BD-22 5866 A"
	Class      "K7 V"
	Radius     427344
	AppMagn    10.1
	MassSol    0.5881
	Orbit
	{
		Period          0.0061
		SemiMajorAxis   0.1755
		Inclination     85.5
		ArgOfPericenter 82
		MeanAnomaly     0
	}
}

Star "BD-22 5866 Ab"
{
	ParentBody "BD-22 5866 A"
	Class      "K7 V"
	Radius     416208
	MassSol    0.5881
	Orbit
	{
		Period          0.0061
		SemiMajorAxis   0.1755
		Inclination     85.5
		ArgOfPericenter 262
		MeanAnomaly     0
	}
}

Star "BD-22 5866 Ba"
{
	ParentBody "BD-22 5866 B"
	Class      "M1 V"
	MassSol    0.49
	Orbit
	{
		Period          0.1699
		SemiMajorAxis   0.14193548
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "BD-22 5866 Bb"
{
	ParentBody "BD-22 5866 B"
	Class      "M2 V"
	MassSol    0.44
	Orbit
	{
		Period          0.1699
		SemiMajorAxis   0.15806452
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//Gliese 867;spanish wiki


Barycenter "Gliese 867 (AB)"
{
	ParentBody "Gliese 867"
	Orbit
	{
		Period          7.8547945205
		SemiMajorAxis   8.3801
		Inclination     60
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Gliese 867 A/HIP 111802/HD 214479"
{
	ParentBody "Gliese 867 (AB)"
	Class      "M1 V"
	Radius     508080
	AppMagn    9.08
	MassSol    0.42
	Orbit
	{
		Period          0.01104658
		SemiMajorAxis   0.0234
		Inclination     60
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Gliese 867 B"
{
	ParentBody "Gliese 867 (AB)"
	Class      "M1 V"
	Radius     361920
	MassSol    0.42
	Orbit
	{
		Period          0.01104658
		SemiMajorAxis   0.0234
		Inclination     60
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "Gliese 867 C"
{
	ParentBody "Gliese 867"
	Class      "M V"
	MassSol    0.26
	Orbit
	{
		Period          7.8548
		SemiMajorAxis   27.0741
		Inclination     60
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


//////////////////////SAGGITARIUS//////////////


Star "Arkab Prior A/HIP 95241/HD 181454"
{
	ParentBody "Arkab Prior"
	Class      "B9 V"
	Radius     4176000
	AppMagn    3.96
	MassSol    3.5
	Orbit
	{
		Period          82000
		SemiMajorAxis   1120.75
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Arkab Prior B"
{
	ParentBody "Arkab Prior"
	Class      "A3 V"
	AppMagn    7.4
	MassSol    1.8
	Orbit
	{
		Period          82000
		SemiMajorAxis   2179.25
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


//Kaus Australis; english wiki


Star "Kaus Australis A/HIP 90185/HD 169022"
{
	ParentBody "Kaus Australis"
	Class      "B9.5 III"
	Radius     4732800
	AppMagn    1.85
	MassSol    3.515
	Orbit
	{
		Period          509.07
		SemiMajorAxis   22.32
		ArgOfPericenter 0
 
		MeanAnomaly     0
	}
}

Star "Kaus Australis B"
{
	ParentBody "Kaus Australis"
	Class      "G V" //unknown, related with Mass
	MassSol    0.95
	Orbit
	{
		Period          509.07
		SemiMajorAxis   82.6
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


//ETA Sgr;english and spanish wiki


Star "ETA Sgr A/HIP 89642/HD 167618"
{
	ParentBody "ETA Sgr"
	Class      "M2 III"
	Radius     43152000
	AppMagn    3.1
	MassSol    1.5
	Orbit
	{
		Period          1283.19
		SemiMajorAxis   73.47
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "ETA Sgr B"
{
	ParentBody "ETA Sgr"
	Class      "F7 V"
	AppMagn    7.8
	Orbit
	{
		Period          1283.19
		SemiMajorAxis   91.07
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//Polis;english and spanish wiki

Star "Polis A/HIP 89341/HD 166937"
{
	ParentBody "Polis"
	Class      "B8 Ia"
	Radius     80040000
	AppMagn    3.84
	MassSol    23
	Orbit
	{
		Period          0.49315068
		SemiMajorAxis   0.65530462
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Polis Ab"
{
	ParentBody "Polis"
	Class      "B1 V"
	Orbit
	{
		Period          0.49315068
		SemiMajorAxis   1.37018238
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//Nunki;english and spanish wiki


Star "Nunki A/SIG Sgr A/HIP 92855/HD 175191"
{
	ParentBody "Nunki"
	Class      "B2 V"
	Radius     3480000
	AppMagn    2.05
	MassSol    7.8
	Orbit
	{
		Period          1071870.71930422
		SemiMajorAxis   2455.80033463
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Nunki B/SIG Sgr B"
{
	ParentBody "Nunki"
	Class      "G V"
	AppMagn    9.5
	Orbit
	{
		Period          1071870.71930422
		SemiMajorAxis   19155.24261015
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


//UPS Sgr;english and spanish wiki


Star "UPS Sgr A/HIP 95176/HD 181615"
{
	ParentBody "UPS Sgr"
	Class      "A5 Ia"
	Radius     41760000
	AppMagn    4.52
	MassSol    25
	Orbit
	{
		Period          0.37780822
		SemiMajorAxis   0.53977273
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "UPS Sgr B"
{
	ParentBody "UPS Sgr"
	Class      "M5 Ia"  //only visible in UV band
	Radius     31760000 //unknown, just fixed     Radius
	AppMagn    20 //invisible in visual band, very low value
	MassSol    19
	Orbit
	{
		Period          0.37780822
		SemiMajorAxis   0.71022727
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//Ascella;6thCVB,english and spanish wiki


Star "Ascella A/ZET Sgr A/HIP 93506/HD 176687"
{
	ParentBody "Ascella"
	Class      "A2 III"
	AppMagn    3.27
	MassSol    2.2
	Orbit
	{
		Period          21
		SemiMajorAxis   6.4465
		Eccentricity    0.211
		Inclination     111.1
		AscendingNode   74
		ArgOfPericenter 7.2
		Epoch           2453732.334169
		MeanAnomaly     0
	}
}

Star "Ascella B/ZET Sgr B"
{
	ParentBody "Ascella"
	Class      "A4 IV"
	AppMagn    3.48
	MassSol    2.1
	Orbit
	{
		Period          21
		SemiMajorAxis   6.7535
		Eccentricity    0.211
		Inclination     111.1
		AscendingNode   74
		ArgOfPericenter 187.2
		Epoch           2453732.334169
		MeanAnomaly     0
	}
}

//9 Sgr;spanish wiki


Star "9 Sgr A/HIP 88469/HD 164794"
{
	ParentBody "9 Sgr"
	Class      "O3 V"
	Radius     11136000
	AppMagn    5.93
	MassSol    55
	Orbit
	{
		Period          8.6
		SemiMajorAxis   5.6592 //for a total Mass of 8 Ms and 8.6y
		Eccentricity    0.7
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "9 Sgr B"
{
	ParentBody "9 Sgr"
	Class      "O5 V"
	MassSol    25
	Orbit
	{
		Period          8.6
		SemiMajorAxis   12.4503
		Eccentricity    0.7
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//GAM1 Sgr;spanish wiki
//2 more companions rejected, unknown if they share common proper motion

Star "GAM1 Sgr Aa/HIP 88567/HD 164975"
{
	ParentBody "GAM1 Sgr"
	Class      "F7 Ib"
	Radius     34800000
	AppMagn    4.66
	MassSol    7
	Orbit
	{
		Period          4.33
		SemiMajorAxis   1.3818
		Eccentricity    0.41
		ArgOfPericenter 0
 
		MeanAnomaly     0
	}
}

Star "GAM1 Sgr Ab"
{
	ParentBody "GAM1 Sgr"
	Class      "A0 V"
	MassSol    2.3
	Orbit
	{
		Period          4.33
		SemiMajorAxis   4.2054
		Eccentricity    0.41
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//Gliese 783;english and spanish wiki


Star "Gliese 783 A/HR 7703 A/LHS 486/LFT 1529/LTT 7988/HIP 99461/HD 191408"
{
	ParentBody "Gliese 783"
	Class      "K2 V"
	Radius     459360
	AppMagn    5.32
	MassSol    0.65
	Orbit
	{
		Period          429.2862
		SemiMajorAxis   15.182
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Gliese 783 B/HR 7703 B/LHS 487/LFT 1530/LTT 7989"
{
	ParentBody "Gliese 783"
	Class      "M4 V"
	Radius     194880
	AppMagn    11.5
	MassSol    0.24
	Orbit
	{
		Period          429.2862
		SemiMajorAxis   41.118
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//BW3 V38;Spanish wiki

Star "BW3 V38 A"
{
	ParentBody "BW3 V38"
	Class      "M3 V"
	Radius     354960
	AppMagn    18.3
	MassSol    0.44
	Orbit
	{
		Period          0.0005
		SemiMajorAxis   0.0029
		ArgOfPericenter 0
 
		MeanAnomaly     0
	}
}

Star "BW3 V38 B"
{
	ParentBody "BW3 V38"
	Class      "M3 V"
	Radius     306240
	MassSol    0.41
	Orbit
	{
		Period          0.0005
		SemiMajorAxis   0.0031
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//RS Sgr;spanish wiki
//AB close binary and eclipsing

Barycenter "RS Sgr (AB)"
{
	ParentBody "RS Sgr"
	Orbit
	{
		Period          440000
		SemiMajorAxis   3272.9275
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "RS Sgr A/HIP 89637/HD 167647"
{
	ParentBody "RS Sgr (AB)"
	Class      "B3 IV"
	Radius     3549600
	AppMagn    6.46
	MassSol    5.1
	Orbit
	{
		Period          0.00935808
		SemiMajorAxis   0.0414
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "RS Sgr B"
{
	ParentBody "RS Sgr (AB)"
	Class      "A2 V"
	Radius     2853600
	MassSol    4.1
	Orbit
	{
		Period          0.00935808
		SemiMajorAxis   0.0516
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "RS Sgr C"
{
	ParentBody "RS Sgr"
	Class      "A1 V"
	AppMagn    9.25
	MassSol    3
	Orbit
	{
		Period          440000
		SemiMajorAxis   10036.9777
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//V350 Sgr;spanish wiki


Star "V350 Sgr A/HIP 92013/HD 173297"
{
	ParentBody "V350 Sgr"
	Class      "F8 Ib"
	Radius     33408000
	AppMagn    7.47
	MassSol    6.3
	Orbit
	{
		Period          4.0603
		SemiMajorAxis   0.215 //unknown
		Eccentricity    0.405
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "V350 Sgr B"
{
	ParentBody "V350 Sgr"
	AppMagn    16 //unknown
	Orbit
	{
		Period          4.0603
		SemiMajorAxis   9.0292 //unknown
		Eccentricity    0.405
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//V505 Sgr;6thCVV,spanish wiki
//Eclipsing binary

Barycenter "V505 Sgr (AB)"
{
	ParentBody "V505 Sgr"
	Orbit
	{
		Period          94.24
		SemiMajorAxis   4.6909
		Eccentricity    0.308
		Inclination     120.4
		AscendingNode   10.1
		ArgOfPericenter 272.3
		Epoch           2451708.892388
		MeanAnomaly     0
	}
}

Star "V505 Sgr A/HIP 97849/HD 187949"
{
	ParentBody "V505 Sgr (AB)"
	Class      "A2 V"
	Radius     1531200
	AppMagn    6.46
	MassSol    5.1
	Orbit
	{
		Period          0.00324027
		SemiMajorAxis   0.0204
		Inclination     120.4   //IN and RA,unknown just aligned
		AscendingNode   10.1
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "V505 Sgr B"
{
	ParentBody "V505 Sgr (AB)"
	Class      "G5 V"
	Radius     1670400
	MassSol    4.1
	Orbit
	{
		Period          0.00324027
		SemiMajorAxis   0.0254
		Inclination     120.4 //IN and RA,unknown just aligned
		AscendingNode   10.1
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "V505 Sgr C"
{
	ParentBody "V505 Sgr"
	Class      "F7 V"
	MassSol    1.2
	Orbit
	{
		Period          94.24
		SemiMajorAxis   35.9637
		Eccentricity    0.308
		Inclination     120.4
		AscendingNode   10.1
		ArgOfPericenter 92.3
		Epoch           2451708.892388
		MeanAnomaly     0
	}
}

//V3903 Sgr;spanish wiki
//eclipsing binary

Star "V3903 Sgr A/HIP 88943/HD 165921"
{
	ParentBody "V3903 Sgr"
	Class      "O7 V"
	Radius     5630640
	AbsMagn    -4.36
	MassSol    27.27
	Orbit
	{
		Period          0.0048
		SemiMajorAxis   0.0411
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "V3903 Sgr B"
{
	ParentBody "V3903 Sgr"
	Class      "O9 V"
	Radius     3243360
	AbsMagn    -3.64
	MassSol    19.01
	Orbit
	{
		Period          0.0048
		SemiMajorAxis   0.0589
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//V3961 Sgr;spanish wiki

Star "V3961 Sgr A/HIP 97749/HD 187474"
{
	ParentBody "V3961 Sgr"
	Class      "A0 V"
	Radius     2505600
	AppMagn    5.32
	MassSol    2.3
	Orbit
	{
		Period          1.8904
		SemiMajorAxis   1.1709 //unknown
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "V3961 Sgr B"
{
	ParentBody "V3961 Sgr"
	AppMagn    11 //unknown
	Orbit
	{
		Period          1.8904
		SemiMajorAxis   1.1709 //unknown
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


//WR 104;Spanish wiki

Star "WR 104 A"
{
	ParentBody "WR 104"
	Class      "WC9"
	Radius     2088000
	AppMagn    13.54
	MassSol    25
	Orbit
	{
		Period          0.6616
		SemiMajorAxis   0.443
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "WR 104 B"
{
	ParentBody "WR 104"
	Class      "B0 V"
	Orbit
	{
		Period          0.6616
		SemiMajorAxis   0.6515
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


//X Sgr;spanish wiki



Star "X Sgr A/HIP 87072/HD 161592"
{
	ParentBody "X Sgr"
	Class      "F7 II"
	Radius     45936000
	AppMagn    4.54
	MassSol    7
	Orbit
	{
		Period          1.5699
		SemiMajorAxis   0.0546 //unknown
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "X Sgr B"
{
	ParentBody "X Sgr"
	AppMagn    9 //unknown
	Orbit
	{
		Period          1.5699
		SemiMajorAxis   2.5462 //unknown
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


////////////////////////////////PISCIS////////////////////////////////////////

//KAP Psc;multiple, unknown data
//OME Psc;false close binary
//DT Psc;unknown mass for primary
//107 Psc;present in 6thCVB but rejected 

//Alrischa;6thCVB,english and spanish wiki

Star "Alrischa A/HIP 9487/HD 12446"
{
	ParentBody "Alrischa"
	Class      "A0 V"
	AppMagn    4.1
	MassSol    2.3
	Orbit
	{
		Period          933.05
		SemiMajorAxis   74.8766
		Eccentricity    0.696
		Inclination     120.9
		AscendingNode   23.3
		ArgOfPericenter 225.4
		Epoch           2487573.119612
		MeanAnomaly     0
	}
}

Star "Alrischa B"
{
	ParentBody "Alrischa"
	Class      "A3 V"
	AppMagn    5.17
	MassSol    1.8
	Orbit
	{
		Period          933.05
		SemiMajorAxis   95.6756
		Eccentricity    0.696
		Inclination     120.9
		AscendingNode   23.3
		ArgOfPericenter 45.4
		Epoch           2487573.119612
		MeanAnomaly     0
	}
}

//ETA Psc;6thCVB,english and spanish wiki


Star "ETA Psc A/HIP 7097/HD 9270"
{
	ParentBody "ETA Psc"
	Class      "G7 II"
	Radius     18096000
	AppMagn    3.83
	MassSol    3.75
	Orbit
	{
		Period          850.5
		SemiMajorAxis   32.829
		Eccentricity    0.469
		Inclination     58.5
		AscendingNode   32.8
		ArgOfPericenter 86.9
		Epoch           2466263.794009
		MeanAnomaly     0
	}
}

Star "ETA Psc B"
{
	ParentBody "ETA Psc"
	Class      "F V"
	AppMagn    7.51
	Orbit
	{
		Period          850.5
		SemiMajorAxis   77.917
		Eccentricity    0.469
		Inclination     58.5
		AscendingNode   32.8
		ArgOfPericenter 266.9
		Epoch           2466263.794009
		MeanAnomaly     0
	}
}


//PSI2 Psc;spanish wiki


Star "PSI2 Psc A/HIP 5310/HD 6695"
{
	ParentBody "PSI2 Psc"
	Class      "A3 V"
	Radius     1392000
	AppMagn    5.57
	MassSol    1.88
	Orbit
	{
		Period          45.0813
		SemiMajorAxis   5.7124
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "PSI2 Psc B"
{
	ParentBody "PSI2 Psc"
	Class      "K V" //unknown related with     AbsMagn
	AppMagn    9.48
	Orbit
	{
		Period          45.0813
		SemiMajorAxis   12.0667
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//33 Psc;spanish wiki


Star "33 Psc A/HIP 443/HD 28"
{
	ParentBody "33 Psc"
	Class      "K0 III"
	Radius     4872000
	AppMagn    4.62
	MassSol    1.7
	Orbit
	{
		Period          0.1998
		SemiMajorAxis   0.1425
		Eccentricity    0.27
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "33 Psc B"
{
	ParentBody "33 Psc"
	Class      "K V" // related with     MassSol, could be also a white dwarf
	MassSol    0.76
	Orbit
	{
		Period          0.1998
		SemiMajorAxis   0.3187
		Eccentricity    0.27
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


//64 Psc;6thCVB,spanish wiki
//very good system

Star "64 Psc A/HIP 3810/HD 4676"
{
	ParentBody "64 Psc"
	Class      "F8 V"
	Radius     870000
	AppMagn    5.1
	MassSol    1.22
	Orbit
	{
		Period          0.0379
		SemiMajorAxis   0.0765
		Eccentricity    0.2376
		Inclination     73.8
		AscendingNode   63.6
		ArgOfPericenter 203.56
		Epoch           2450905.984
		MeanAnomaly     0
	}
}

Star "64 Psc B"
{
	ParentBody "64 Psc"
	Class      "F8 V"
	Radius     821280
	AppMagn    5.2
	MassSol    1.17
	Orbit
	{
		Period          0.0379
		SemiMajorAxis   0.0797
		Eccentricity    0.2376
		Inclination     73.8
		AscendingNode   63.6
		ArgOfPericenter 23.56
		Epoch           2450905.984
		MeanAnomaly     0
	}
}

//BE Psc;spanish wiki


Star "BE Psc A/HIP 5007/HD 6286"
{
	ParentBody "BE Psc"
	Class      "K1 III"
	Radius     8352000
	AppMagn    8.24
	MassSol    1.56
	Orbit
	{
		Period          0.0977
		SemiMajorAxis   0.1375
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "BE Psc B"
{
	ParentBody "BE Psc"
	Class      "F6 V"
	Radius     1322400
	MassSol    1.31
	Orbit
	{
		Period          0.0977
		SemiMajorAxis   0.1638
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


//UU Psc;spanish wiki,prof. jim kaler website


Barycenter "UU Psc (AB)"
{
	ParentBody "UU Psc"
	Orbit
	{
		Period          12123.17
		SemiMajorAxis   272.0011
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "UU Psc A/HIP 1196/HD 1061"
{
	ParentBody "UU Psc (AB)"
	Class      "F0 V"
	Radius     1078800
	AppMagn    6.54
	MassSol    1.6
	Orbit
	{
		Period          0.00230685
		SemiMajorAxis   0.0129
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "UU Psc B"
{
	ParentBody "UU Psc (AB)"
	Class      "F0 V"
	Radius     1078800
	AppMagn    6.54
	MassSol    1.6
	Orbit
	{
		Period          0.00230685
		SemiMajorAxis   0.0129
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "UU Psc C"
{
	ParentBody "UU Psc"
	Class      "F3 V"
	AppMagn    7.51
	Orbit
	{
		Period          12123.17
		SemiMajorAxis   608.6738
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


//UV Psc;spanish wiki


Star "UV Psc A/HIP 5980/HD 7700"
{
	ParentBody "UV Psc"
	Class      "G5 V"
	Radius     772560
	AbsMagn    4.58
	MassSol    0.98
	Orbit
	{
		Period          0.0024
		SemiMajorAxis   0.0093
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "UV Psc B"
{
	ParentBody "UV Psc"
	Class      "K3 V"
	Radius     584640
	AbsMagn    6.41
	MassSol    0.76
	Orbit
	{
		Period          0.0024
		SemiMajorAxis   0.012
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//Y Psc;spanish wiki


Star "Y Psc A/HIP 116339/HD 221700"
{
	ParentBody "Y Psc"
	Class      "A3 V"
	Radius     2157600
	AbsMagn    0.08
	MassSol    2.8
	Orbit
	{
		Period          0.0103
		SemiMajorAxis   0.0144
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Y Psc B"
{
	ParentBody "Y Psc"
	Class      "K0 IV"
	Radius     2784000
	MassSol    0.7
	Orbit
	{
		Period          0.0103
		SemiMajorAxis   0.0575
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


//////////////////////////OPHIUCHUS///////////////////////

Barycenter	"36 Oph (AB)/HIP 84405"
{
	ParentBody "36 Oph"
	Orbit
	{
		Period          225000		// calculated
		SemiMajorAxis	1512		// mass ratio 0.75:1.67 and a = 4880
		Eccentricity	0.1045082	// derived from periapris = 4370AU and apoapsis = 5390 AU
		Inclination		85.0		// random
		AscendingNode	-83.6		// copy of the AB pair data
		ArgOfPericenter	240
		MeanAnomaly		0
	}
}

Star	"36 Oph A/Gliese 663 A/GJ 663 A/HR 6402/HD 155886/SAO 185198"
{
	ParentBody	"36 Oph (AB)"
	Class		"K2V"
	AppMagn		5.59
	Age			1.43
	MassSol		0.85
	RadSol		0.817
	Teff        5125
	FeH			-0.2

	Orbit
	{
		Epoch			2401763.39
		Period			568.9
		SemiMajorAxis	44	// mass ratio 1:1
		Eccentricity	0.922
		Inclination		99.6
		AscendingNode	-83.6
		ArgOfPericenter	0
		MeanAnomaly		0
	}
}

Star	"36 Oph B/Gliese 663 B/GJ 663 B/HR 6401/HD 155885/SAO 185199"
{
	ParentBody	"36 Oph (AB)"
	Class		"K1V"
	AppMagn		5.33
	Age			1.43
	MassSol		0.82
	RadSol		0.81
	Teff        5100
	FeH			-0.31

	Orbit
	{
		Epoch			2401763.39
		Period			568.9
		SemiMajorAxis	44	// mass ratio 1:1
		Eccentricity	0.922
		Inclination		99.6
		AscendingNode	-83.6
		ArgOfPericenter	180
		MeanAnomaly		0
	}
}

Star	"36 Oph C/Gliese 664/GJ 664/HD 156026/SAO 185213"
{
	ParentBody	"36 Oph"
	Class		"K5V"
	AbsMagn		6.34
	Age			0.59
	MassSol		0.75
	RadSol		0.72
	Teff        4550
	FeH			-0.2

	Orbit
	{
		Period          225000		// calculated
		SemiMajorAxis	3368		// mass ratio 0.75:1.67 and a = 4880
		Eccentricity	0.1045082	// derived from periapris = 4370AU and apoapsis = 5390 AU
		Inclination		85.0		// random
		AscendingNode	-83.6		// copy of the AB pair data
		ArgOfPericenter	60
		MeanAnomaly		0
	}
}

//Rasalhague;6thCVB,english,spanish wiki
//very good system

Star "Rasalhague A/ALF Oph A/HIP 86032/HD 159561"
{
	ParentBody "Rasalhague"
	Class      "A5 III"
	Radius     1809600
	AppMagn    2.1
	MassSol    2.4
	Orbit
	{
		Period          8.6258
		SemiMajorAxis   1.6649
		Eccentricity    0.92
		Inclination     125
		AscendingNode   232
		ArgOfPericenter 162
		Epoch           2452888
		MeanAnomaly     0
	}
}

Star "Rasalhague B/ALF Oph B"
{
	ParentBody "Rasalhague"
	Class      "K5 V"
	AppMagn    5
	MassSol    0.85
	Orbit
	{
		Period          8.6258
		SemiMajorAxis   4.7008
		Eccentricity    0.92
		Inclination     125
		AscendingNode   232
		ArgOfPericenter 342
		Epoch           2452888
		MeanAnomaly     0
	}
}

//CHI Oph;spanish wiki
//semiaxis and period known,for a Mass for the primary of 9 MS
//the Mass for the companion should be around 0.19 MS (3rd kepler law)
//according to a M dwarf or a white dwarf

Star "CHI Oph A/7 Oph A/HIP 80569/HD 148184"
{
	ParentBody "CHI Oph"
	Class      "B2 V"
	Radius     5568000
	AppMagn    4.42
	MassSol    9
	Orbit
	{
		Period          0.3808
		SemiMajorAxis   0.0227
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "CHI Oph B/7 Oph B"
{
	ParentBody "CHI Oph"
	Class      "M V" //unknown, according its Mass, also could be a white dwarf
	MassSol    0.19
	Orbit
	{
		Period          0.3808
		SemiMajorAxis   1.0773
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//Sabik;6thCVB,spanish and english wiki
//very good system

Star "Sabik A/HIP 84012/HD 155125"
{
	ParentBody "Sabik"
	Class      "A1 V"
	Radius     1740000
	AppMagn    3.05
	MassSol    2.3
	Orbit
	{
		Period          87.58
		SemiMajorAxis   17.5272
		Eccentricity    0.95
		Inclination     95.2
		AscendingNode   38.9
		ArgOfPericenter 274.8
		Epoch           2460558.710864
		MeanAnomaly     0
	}
}

Star "Sabik B"
{
	ParentBody "Sabik"
	Class      "A3 V"
	Radius     1392000
	AppMagn    3.27
	MassSol    2
	Orbit
	{
		Period          87.58
		SemiMajorAxis   20.1563
		Eccentricity    0.95
		Inclination     95.2
		AscendingNode   38.9
		ArgOfPericenter 94.8
		Epoch           2460558.710864
		MeanAnomaly     0
	}
}

//Marfik;6thCVB,english and spanish wiki


Barycenter "Marfik (AB)"
{
	ParentBody "Marfik"
	Orbit
	{
		Period          207087.14
		SemiMajorAxis   807.0379
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Marfik A/HIP 80883/HD 148857"
{
	ParentBody "Marfik (AB)"
	Class      "A1 V"
	Radius     1740000
	AppMagn    4.16
	MassSol    2.6
	Orbit
	{
		Period          129
		SemiMajorAxis   20.1467
		Eccentricity    0.611
		Inclination     23
		AscendingNode   53.3
		ArgOfPericenter 157.5
		Epoch           2429520.428812
		MeanAnomaly     0
	}
}

Star "Marfik B"
{
	ParentBody "Marfik (AB)"
	Class      "A4 V"
	Radius     1322400
	AppMagn    5.22
	MassSol    2
	Orbit
	{
		Period          129
		SemiMajorAxis   26.1907
		Eccentricity    0.611
		Inclination     23
		AscendingNode   53.3
		ArgOfPericenter 337.5
		Epoch           2429520.428812
		MeanAnomaly     0
	}
}

Star "Marfik C"
{
	ParentBody "Marfik"
	Class      "K6 V"
	AppMagn    11
	Orbit
	{
		Period          207087.14
		SemiMajorAxis   5303.3916
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//70 Oph;6thCVB,english and spanish wiki


Star "70 Oph A/HR 6752/LHS 458/HIP 88601/HD 165341"
{
	ParentBody "70 Oph"
	Class      "K0 V"
	Radius     591600
	AppMagn    4.22
	MassSol    0.89
	Orbit
	{
		Period          88.4356
		SemiMajorAxis   10.2269
		Eccentricity    0.5005
		Inclination     121.1
		AscendingNode   121.7
		ArgOfPericenter 193.4
		Epoch           2445809
		MeanAnomaly     0
	}
}

Star "70 Oph B/LHS 459"
{
	ParentBody "70 Oph"
	Class      "K4 V"
	Radius     487200
	AppMagn    6.2
	MassSol    0.71
	Orbit
	{
		Period          88.4356
		SemiMajorAxis   12.8196
		Eccentricity    0.5005
		Inclination     121.1
		AscendingNode   121.7
		ArgOfPericenter 13.4
		Epoch           2445809
		MeanAnomaly     0
	}
}

//Gliese 644;6thCVB,english and spanish wiki

Barycenter "Gliese 644 (ABC)"
{
	ParentBody "Gliese 644"
	Orbit
	{
		Period          46319.7248
		SemiMajorAxis   104.0365
		Inclination     161
		AscendingNode   147
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}


Barycenter "Gliese 644 (AB)"
{
	ParentBody "Gliese 644 (ABC)"
	Orbit
	{
		Period          9009.6891
		SemiMajorAxis   71.2291
		Inclination     161
		AscendingNode   147
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "Gliese 644 A/V1054 Oph A"
{
	ParentBody "Gliese 644 (AB)"
	Class      "M2 V"
	AppMagn    9.8
	MassSol    0.41
	Orbit
	{
		Period          1.717
		SemiMajorAxis   0.5798
		Eccentricity    0.06
		Inclination     161
		AscendingNode   147
		ArgOfPericenter 104
		Epoch           2448476.498928
		MeanAnomaly     0
	}
}


Barycenter "Gliese 644 B"
{
	ParentBody "Gliese 644 (AB)"
	Orbit
	{
		Period          1.717
		SemiMajorAxis   0.9051
		Eccentricity    0.06
		Inclination     161
		AscendingNode   147
		ArgOfPericenter 284
		Epoch           2448476.498928
		MeanAnomaly     0
	}
}



Star "Gliese 644 Ba/V1054 Oph Ba"
{
	ParentBody "Gliese 644 B"
	Class      "M4 V"
	AppMagn    9.8
	MassSol    0.34
	Orbit
	{
		Period          0.0081
		SemiMajorAxis   0.2078
		Eccentricity    0.06
		Inclination     164.18
		ArgOfPericenter 150
		Epoch           2450919.48
		MeanAnomaly     0
	}
}

Star "Gliese 644 Bb/V1054 Oph Bb"
{
	ParentBody "Gliese 644 B"
	Class      "M4 V"
	MassSol    0.3
	Orbit
	{
		Period          0.0081
		SemiMajorAxis   0.2355
		Eccentricity    0.06
		Inclination     164.18
		ArgOfPericenter 330
		Epoch           2450919.48
		MeanAnomaly     0
	}
}


Star "Gliese 643/GJ 643/Wolf 629/LHS 427"
{
	ParentBody "Gliese 644 (ABC)"
	Class      "M3.5 V"
	AppMagn    11.7
	MassSol    0.19
	Orbit
	{
		Period          9009.6891
		SemiMajorAxis   393.6347
		Inclination     161
		AscendingNode   147
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Gliese 644 D/Gliese 644 C/V1054 Oph C/VB 8/LHS 429"
{
	ParentBody "Gliese 644"
	Class      "M7 V"
	AbsMagn    17.75
	MassSol    0.08
	Orbit
	{
		Period          46319.7248
		SemiMajorAxis   1316.3806
		Inclination     161
		AscendingNode   147
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//71 Oph;spanish wiki,simbad
//unknown hierarchy

Star "71 Oph A/HIP 88771/HD 165777"
{
	ParentBody "71 Oph"
	Class      "A4 IV"
	Radius     1461600
	AppMagn    3.72
	MassSol    2.3
	Orbit
	{
		Period          88.7
		SemiMajorAxis   0.6424
		ArgOfPericenter 0
		MeanAnomaly     180
	}
}

Star "71 Oph B"
{
	ParentBody "71 Oph"
	Class      "K V"
	AppMagn    8.92
	Orbit
	{
		Period          88.7
		SemiMajorAxis   28.1876
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

/*

Star "71 Oph C"
{
	ParentBody "71 Oph"
	Class      "M V"
	AppMagn    11.82
	Orbit
	{
		SemiMajorAxis   24.2
		ArgOfPericenter 0
		MeanAnomaly     180
	}
}

*/

//G 21-15;WD STAR PRESENT

//Gliese 688;6thCVB,spanish wiki
//replaced the 6thCVB semiaxis data for other more acoording
//the period and the Mass system, both confirmed

Star "Gliese 688 A/HIP 86400/HD 160346"
{
	ParentBody "Gliese 688"
	Class      "K3 V"
	Radius     6152640
	AppMagn    6.52
	MassSol    0.78
	Orbit
	{
		Period          0.2293
		SemiMajorAxis   0.037
		Eccentricity    0.23
		Inclination     18.4
		AscendingNode   274.2
		ArgOfPericenter 140.5
		Epoch           2447724.9
		MeanAnomaly     0
	}
}

Star "Gliese 688 B"
{
	ParentBody "Gliese 688"
	Class      "M V" //unknown, most probably a red dwarf
	MassSol    0.09  //minimun Mass
	Orbit
	{
		Period          0.2293
		SemiMajorAxis   0.3205
		Eccentricity    0.23
		Inclination     18.4
		AscendingNode   274.2
		ArgOfPericenter 320.5
		Epoch           2447724.9
		MeanAnomaly     0
	}
}

//Gliese 653;spanish wiki

Star "Gliese 653/Wolf 635/HIP 83591/HD 154363"
{
	ParentBody "Gliese 653 (AB)"
	Class      "K5 V"
	AppMagn    7.7
	MassSol    0.68
	Orbit
	{
		Period          90811.0313
		SemiMajorAxis   512.3086
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Gliese 654/Wolf 636/HIP 83599/NSV 8176"
{
	ParentBody "Gliese 653 (AB)"
	Class      "M3 V"
	AppMagn    10.07
	Orbit
	{
		Period          90811.0313
		SemiMajorAxis   1451.5411
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//RHO Ophi;english and spanish wiki

Star "RHO Oph A/HIP 80473"
{
	ParentBody "RHO Oph"
	Class      "B2 IV" 
	AppMagn    4.63
	MassSol    9
	Orbit
	{
		Period          2000
		SemiMajorAxis   161.8824
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "RHO Oph B"
{
	ParentBody "RHO Oph"
	Class      "B2 V"
	MassSol    8
	Orbit
	{
		Period          2000
		SemiMajorAxis   182.1176
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//RS Oph;WD STAR PRESENT


//TET Oph;english and spanish wiki, prof jim kaller website


Barycenter "TET Oph (AB)"
{
	ParentBody "TET Oph"
	Orbit
	{
		Period          0.2503506175
		SemiMajorAxis   4.4797
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "TET Oph A/HIP 84970/HD 157056"
{
	ParentBody "TET Oph (AB)"
	Class      "B2 IV"
	Radius     4384800
	AppMagn    3.26
	MassSol    8.8
	Orbit
	{
		Period          0.03134247
		SemiMajorAxis   0.1125 //using prof. jim kaller separation
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "TET Oph B"
{
	ParentBody "TET Oph (AB)"
	Class      "B V" //unknown, for a total system Mass of 16 MS, maybe a high Mass star
	Orbit
	{
		Period          0.03134247
		SemiMajorAxis   0.1375
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "TET Oph C"
{
	ParentBody "TET Oph"
	Class      "B5 V"
	AppMagn    5.5
	Orbit
	{
		Period          0.2504
		SemiMajorAxis   15.5816
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//U Oph;spanish wiki
//inner components, eclipsing binary


Barycenter "U Oph (AB)"
{
	ParentBody "U Oph"
	Orbit
	{
		Period          21
		SemiMajorAxis   1.42180416
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Barycenter "U Oph (CD)"
{
	ParentBody "U Oph"
	Orbit
	{
		Period          21
		SemiMajorAxis   4.80819584
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "U Oph A/HIP 84500/HD 156247"
{
	ParentBody "U Oph (AB)"
	Class      "B5 V"
	Radius     2436000
	AbsMagn    -1.07
	MassSol    5.27
	Orbit
	{
		Period          0.0046
		SemiMajorAxis   0.02824307
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "U Oph B"
{
	ParentBody "U Oph (AB)"
	Class      "B6 V"
	Radius     2157600
	AbsMagn    -0.71
	MassSol    4.74
	Orbit
	{
		Period          0.0046
		SemiMajorAxis   0.03140105
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "U Oph C"
{
	ParentBody "U Oph (CD)"
	Class      "F V"   //unknown, related with Mass
	Radius     1009200
	MassSol    1.48
	Orbit
	{
		Period          0.5877 //unknown
		SemiMajorAxis   0.5 //unknown
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "U Oph D"
{
	ParentBody "U Oph (CD)"
	Class      "F V" //unknown, related with Mass
	Radius     1009200
	MassSol    1.48
	Orbit
	{
		Period          0.5877   //unknown
		SemiMajorAxis   0.5 //unknown
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//UPS Oph;6thCVB, spanish wiki
//very good system


Star "UPS Oph A/HIP 80628/HD 148367"
{
	ParentBody "UPS Oph"
	Class      "A3 V"
	Radius     1461600
	AppMagn    4.71
	Orbit
	{
		Period          82.8
		SemiMajorAxis   13.7967
		Eccentricity    0.45
		Inclination     31.2
		AscendingNode   86.8
		ArgOfPericenter 177.9
		Epoch           2449389.604425
		MeanAnomaly     0
	}
}

Star "UPS Oph B"
{
	ParentBody "UPS Oph"
	Class      "A V" //unknown related with a system Mass of 3.75
	AppMagn    8.83
	Orbit
	{
		Period          82.8
		SemiMajorAxis   15.7677
		Eccentricity    0.45
		Inclination     31.2
		AscendingNode   86.8
		ArgOfPericenter 357.9
		Epoch           2449389.604425
		MeanAnomaly     0
	}
}


//V2129 Oph;spanish wiki
//pre main sequence stars


Star "V2129 Oph A"
{
	ParentBody "V2129 Oph"
	Class      "K5 V" //not yet in the main sequence
	Radius     1392000
	AppMagn    11.2
	MassSol    1.35
	Orbit
	{
		Period          16045.4449
		SemiMajorAxis   49.6298
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "2MASS J16262096-2408568"
{
	ParentBody "V2129 Oph"
	Class      "M V" //not yet in the main sequence
	MassSol    0.1
	Orbit
	{
		Period          16045.4449
		SemiMajorAxis   670.0021
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//V2388;6thCVB spanish wiki
//AaAb contacting binary


Barycenter "V2388 Oph A"
{
	ParentBody "V2388 Oph"
	Orbit
	{
		Period          9.008
		SemiMajorAxis   1.63
		Eccentricity    0.327
		Inclination     160.9
		AscendingNode   173.6
		ArgOfPericenter 58.3
		Epoch           2457499.807449
		MeanAnomaly     0
	}
}

Star "V2388 Oph Aa/HIP 87655/HD 163151"
{
	ParentBody "V2388 Oph A"
	Class      "F3 V" //total spectra for both contacting binaries
	Radius     1809600
	AbsMagn    1.95
	MassSol    1.8
	Orbit
	{
		Period          0.00219808
		SemiMajorAxis   0.0035
		Inclination     160.9  //IN and AN unknown, just aligned
		AscendingNode   173.6
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "V2388 Oph Ab"
{
	ParentBody "V2388 Oph A"
	Class      "F V"
	Radius     904800
	AbsMagn    3.82
	MassSol    0.34
	Orbit
	{
		Period          0.00219808
		SemiMajorAxis   0.0183
		Inclination     160.9   //IN and AN unknown, just aligned
		AscendingNode   173.6
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "V2388 Oph B"
{
	ParentBody "V2388 Oph"
	Class      "K V" //unknown related with its Mass
	AppMagn    7.6
	MassSol    0.84
	Orbit
	{
		Period          9.008
		SemiMajorAxis   4.1526
		Eccentricity    0.327
		Inclination     160.9
		AscendingNode   173.6
		ArgOfPericenter 238.3
		Epoch           2457499.807449
		MeanAnomaly     0
	}
}


//X Oph;6thCVB;
//astronomical article: The binary system x ophiuchi
//J.D. Fernie 4/10/1959
//and the article: Variable Star of the Year 2014 X Ophiuchi
//unknown author, I used the Mass and separation of the last


Star "X Oph A/HIP 91389/HD 172171"
{
	ParentBody "X Oph"
	Class      "K1 III"
	AppMagn    8.5
	MassSol    2.96
	Orbit
	{
		Period          241.5649
		SemiMajorAxis   21.8085
		Eccentricity    0.446
		Inclination     103.7
		AscendingNode   130.1
		ArgOfPericenter 14.1
		Epoch           2459207.314729
		MeanAnomaly     0
	}
}

Star "X Oph B"
{
	ParentBody "X Oph"
	Class      "M6 III"
	AppMagn    8.6 		//Mira variable
	MassSol    1.53
	Orbit
	{
		Period          241.5649
		SemiMajorAxis   42.1915
		Eccentricity    0.446
		Inclination     103.7
		AscendingNode   130.1
		ArgOfPericenter 194.1
		Epoch           2459207.314729
		MeanAnomaly     0
	}
}


///////////////////////////AURIGA////////////////////////////////////////


//Capella;6thCVB,english and spanish wiki

Barycenter "Capella (AB)"
{
	ParentBody "Capella"
	Orbit
	{
		Period          472576
		SemiMajorAxis   1326.63316583
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Barycenter "Capella (HL)"
{
	ParentBody "Capella"
	Orbit
	{
		Period          472576
		SemiMajorAxis   9673.36683417
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}



Star "Capella A/HIP 24608/HD 34029"
{
	ParentBody "Capella (AB)"
	Class      "G8 III"
	Radius     8491200
	AppMagn    0.35
	MassSol    2.69
	Orbit
	{
		Period          0.285
		SemiMajorAxis   0.35644563
		Inclination     137.18
		AscendingNode   40.8
		ArgOfPericenter 0
		Epoch           2447528.45
		MeanAnomaly     0
	}
}

Star "Capella B"
{
	ParentBody "Capella (AB)"
	Class      "G1 III"
	Radius     6403200
	AppMagn    0.2
	MassSol    2.56
	Orbit
	{
		Period          0.285
		SemiMajorAxis   0.37454639
		Inclination     137.18
		AscendingNode   40.8
		ArgOfPericenter 180
		Epoch           2447528.45
		MeanAnomaly     0
	}
}

Star "Capella H"
{
	ParentBody "Capella (HL)"
	Class      "M1 V"
	Radius     375840
	AppMagn    10.16
	MassSol    0.53
	Orbit
	{
		Period          388
		SemiMajorAxis   12.70746421
		Inclination     65
		AscendingNode   168.5
		ArgOfPericenter 0
		Epoch           2455196.955386
		MeanAnomaly     0
	}
}

Star "Capella L"
{
	ParentBody "Capella (HL)"
	Class      "M5 V"
	AppMagn    13.7
	MassSol    0.19
	Orbit
	{
		Period          388
		SemiMajorAxis   35.44713701
		Inclination     65
		AscendingNode   168.5
		ArgOfPericenter 180
		Epoch           2455196.955386
		MeanAnomaly     0
	}
}

//45 Aur;spanish wiki

Star "45 Aur A/HD 43905"
{
	ParentBody "45 Aur"
	Class      "F5 V"
	AppMagn    5.35
	MassSol    1.48
	Orbit
	{
		Period          0.0178
		SemiMajorAxis   0.0187
		Eccentricity    0.02
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "45 Aur B"
{
	ParentBody "45 Aur"
	Class      "M V" //unknown could be also a white dwarf
	MassSol    0.42   //confirmed
	Orbit
	{
		Period          0.0178
		SemiMajorAxis   0.0658
		Eccentricity    0.02
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//Menkalinan;6thCVB,spanish wiki


Barycenter "Menkalinan (AB)"
{
	ParentBody "Menkalinan"
	Orbit
	{
		Period          2732.86
		SemiMajorAxis   10.2697
		Inclination     76
		AscendingNode   115.4
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Menkalinan A/HIP 28360/HD 40183"
{
	ParentBody "Menkalinan (AB)"
	Class      "A1 IV"
	Radius     1920960
	AppMagn    2.56
	MassSol    2.38
	Orbit
	{
		Period          0.01084932
		SemiMajorAxis   0.0403
		Inclination     76
		AscendingNode   115.4
		ArgOfPericenter 0
		Epoch           2447438.65
		MeanAnomaly     0
	}
}

Star "Menkalinan B"
{
	ParentBody "Menkalinan (AB)"
	Class      "A1 IV"
	Radius     1788720
	AppMagn    2.8
	MassSol    2.29
	Orbit
	{
		Period          0.01084932
		SemiMajorAxis   0.0418
		Inclination     76
		AscendingNode   115.4
		ArgOfPericenter 180
		Epoch           2447438.65
		MeanAnomaly     0
	}
}

Star "Menkalinan C"
{
	ParentBody "Menkalinan"
	Class      "M V"
	AppMagn    14.1
	MassSol    0.15
	Orbit
	{
		Period          2732.86
		SemiMajorAxis   319.7303
		Inclination     76
		AscendingNode   115.4
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


//Almaaz;6thCVB, english wiki
//very good system


Star "Almaaz A/HIP 23416/HD 31964"
{
	ParentBody "Almaaz"
	Class      "F0 Ib"
	Radius     132240000
	AppMagn    3.04
	MassSol    15 //unknown;maximum
	Orbit
	{
		Period          27.0877
		SemiMajorAxis   6.6342
		Eccentricity    0.07
		Inclination     87
		AscendingNode   264
		ArgOfPericenter 0
		Epoch           2433373.5
		MeanAnomaly     0
	}
}

Star "Almaaz B"
{
	ParentBody "Almaaz"
	Class      "B5 V"
	Radius     2714400
	MassSol    14 // unknown, maximum
	Orbit
	{
		Period          27.0877
		SemiMajorAxis   7.1081
		Eccentricity    0.07
		Inclination     87
		AscendingNode   264
		ArgOfPericenter 180
		Epoch           2433373.5
		MeanAnomaly     0
	}
}


//OME Aur;english and spanish wiki

Star "OME Aur A/HIP 23179/HD 31647"
{
	ParentBody "OME Aur"
	Class      "A1 V"
	Radius     1252800
	AppMagn    4.99
	Orbit
	{
		Period          1747.1845
		SemiMajorAxis   73.9695
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "OME Aur B"
{
	ParentBody "OME Aur"
	Class      "F9 V"
	AppMagn    8.1
	Orbit
	{
		Period          1747.1845
		SemiMajorAxis   142.7482
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


//Gliese 268;6thCVB,spanish wiki
//very good system, close to sun


Star "Gliese 268 A/HIP 34603"
{
	ParentBody "Gliese 268"
	Class      "M5 V"
	AppMagn    12.05
	MassSol    0.17
	Orbit
	{
		Period          0.0286
		SemiMajorAxis   0.033
		Eccentricity    0.3203
		Inclination     100.39
		AscendingNode   89.98
		ArgOfPericenter 211.98
		Epoch           2450493.9853
		MeanAnomaly     0
	}
}

Star "Gliese 268 B"
{
	ParentBody "Gliese 268"
	Class      "M5 V"
	AppMagn    12.45
	MassSol    0.16
	Orbit
	{
		Period          0.0286
		SemiMajorAxis   0.0351
		Eccentricity    0.3203
		Inclination     100.39
		AscendingNode   89.98
		ArgOfPericenter 31.98
		Epoch           2450493.9853
		MeanAnomaly     0
	}
}

//TET Aur;english and spanish wiki


Star "TET Aur A/HIP 28380/HD 40312"
{
	ParentBody "TET Aur"
	Class      "A0 V"
	Radius     3549600
	AppMagn    2.62
	MassSol    3.38
	Orbit
	{
		Period          1134
		SemiMajorAxis   59.4816
		ArgOfPericenter 0
 
		MeanAnomaly     0
	}
}

Star "TET Aur B"
{
	ParentBody "TET Aur"
	Class      "F2 V"
	AppMagn    7.2
	Orbit
	{
		Period          1134
		SemiMajorAxis   139.6166
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//V394 Aur;spanish wiki

Star "V394 Aur A/HD 41429"
{
	ParentBody "V394 Aur"
	Class      "M3 II"
	Radius     164256000
	AppMagn    6.08
	MassSol    0.93  //low     MassSol    star but giant
	Orbit
	{
		Period          72050.7463
		SemiMajorAxis   1260.9225
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "V394 Aur B"
{
	ParentBody "V394 Aur"
	Class      "F7 V"
	AppMagn    10.3
	Orbit
	{
		Period          72050.7463
		SemiMajorAxis   969.1388
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


//V398 Aur;spanish wiki


Star "V398 Aur A/Gliese 9174 A/GJ 9174 A/HD 32537/HIP 26779"
{
	ParentBody "V398 Aur"
	Class      "F0 V"
	Radius     1113600
	AppMagn    4.98
	MassSol    1.4
	Orbit
	{
		Period          1100
		SemiMajorAxis   47.7907
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "V398 Aur B/Gliese 9174 B/GJ 9174 B/HIP 26801"
{
	ParentBody "V398 Aur"
	Class      "M2 V"
	AppMagn    12.2
	MassSol    0.75
	Orbit
	{
		Period          1100
		SemiMajorAxis   89.2093
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


//V538 Aur;spanish wiki


Star "Gliese 211/HR 1925/HD 37394"
{
	ParentBody "V538 Aur"
	Class      "K1 V"
	Radius     570720
	AppMagn    6.23
	MassSol    0.91
	Orbit
	{
		Period          34304
		SemiMajorAxis   463.1073
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Gliese 212/LHS 1775"
{
	ParentBody "V538 Aur"
	Class      "M0 V"
	Radius     494160
	AppMagn    9.87
	MassSol    0.57
	Orbit
	{
		Period          34304
		SemiMajorAxis   739.3467
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


//WW Aur;spanish wiki
//eclipsing binary

Star "WW Aur A/HIP 31173/HD 46052"
{
	ParentBody "WW Aur"
	Class      "A3 V"
	Radius     1336320
	AppMagn    5.86
	MassSol    1.96
	Orbit
	{
		Period          0.0069
		SemiMajorAxis   0.0271
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "WW Aur B"
{
	ParentBody "WW Aur"
	Class      "A3 V"
	Radius     1280640
	MassSol    1.81
	Orbit
	{
		Period          0.0069
		SemiMajorAxis   0.0294
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//Haedus;6thCVB,spanish,english wiki
//very good system

Star "Haedus A/HIP 23453/HD 32068"
{
	ParentBody "Haedus"
	Class      "K5 Ib"
	Radius     103008000
	AppMagn    3.75
	MassSol    4.94
	Orbit
	{
		Period          2.6635
		SemiMajorAxis   1.9347
		Eccentricity    0.37
		Inclination     87.3
		AscendingNode   163.9
		ArgOfPericenter 330
		Epoch           2447212
		MeanAnomaly     0
	}
}

Star "Haedus B"
{
	ParentBody "Haedus"
	Class      "B5 V"
	Radius     3132000
	MassSol    4.8
	Orbit
	{
		Period          2.6635
		SemiMajorAxis   1.9911
		Eccentricity    0.37
		Inclination     87.3
		AscendingNode   163.9
		ArgOfPericenter 150
		Epoch           2447212
		MeanAnomaly     0
	}
}

//////////////////////////////CETUS//////////////////////////////////////////////


//Mira;6thCVB,english and spanish wiki
//very good system

Star "Mira A/HIP 10826/HD 14386"
{
	ParentBody "Mira"
	Class      "M7 III"
	AppMagn      6.47
	MassSol      1.18
	RadSol       350
	Teff         3000
	Age          6
	Orbit
	{
		Period          497.88
		SemiMajorAxis   27.3202
		Eccentricity    0.16
		Inclination     112
		AscendingNode   138.8
		ArgOfPericenter 258.3
		Epoch           2555912.4917
		MeanAnomaly     0
	}
}

Star "Mira B/VZ Cet"
{
	ParentBody "Mira"
	Class      "DA1" 
	AppMagn    10.4
	MassSol    0.7
	Orbit
	{
		Period          497.88
		SemiMajorAxis   46.054
		Eccentricity    0.16
		Inclination     112
		AscendingNode   138.8
		ArgOfPericenter 78.3
		Epoch           2555912.4917
		MeanAnomaly     0
	}
}

//13 Cet;6thCVB,spanish wiki

Barycenter "13 Cet A"
{
	ParentBody "13 Cet"
	Orbit
	{
		Period          6.89
		SemiMajorAxis   2.0162
		Eccentricity    0.773
		Inclination     49.4
		AscendingNode   149
		ArgOfPericenter 283.8
		Epoch           2451902.470753
		MeanAnomaly     0
	}
}

Star "13 Cet Aa/HIP 2762/HD 3196"
{
	ParentBody "13 Cet A"
	Class      "F8 V"
 
	AppMagn    5.2
	MassSol    1.18
	Orbit
	{
		Period          0.00569863
		SemiMajorAxis   0.0084
		Inclination     49.4   //unknown, RA and IN just aligned
		AscendingNode   149
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "13 Cet Ab"
{
	ParentBody "13 Cet A"
	Class      "M V" //unknown, could be also a WD
	MassSol    0.35  //confirmed
	Orbit
	{
		Period          0.00569863
		SemiMajorAxis   0.0283
		Inclination     49.4   //unknown, RA and IN just aligned
		AscendingNode   149
		ArgOfPericenter 180
 
		MeanAnomaly     0
	}
}

Star "13 Cet B"
{
	ParentBody "13 Cet"
	Class      "G2 V"
	MassSol    1
	Orbit
	{
		Period          6.89
		SemiMajorAxis   3.0847
		Eccentricity    0.773
		Inclination     49.4
		AscendingNode   149
		ArgOfPericenter 103.8
		Epoch           2451902.470753
		MeanAnomaly     0
	}
}

//94 Cet;with exoplanet

//AB Cet;WD STAR PRESENT

//GAM Cet;english, spanish wiki


Barycenter "GAM Cet (AB)"
{
	ParentBody "GAM Cet"
	Orbit
	{
		Period          1471872.37
		SemiMajorAxis   3817.3142
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "GAM Cet A/HIP 12706/HD 16970"
{
	ParentBody "GAM Cet (AB)"
	Class      "A3 V"
	Radius     1322400
	AppMagn    3.56
	MassSol    2
	Orbit
	{
		Period          312.94795804
		SemiMajorAxis   27.0682
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "GAM Cet B"
{
	ParentBody "GAM Cet (AB)"
	Class      "F3 V"
	AppMagn    6.63
	MassSol    1.3
	Orbit
	{
		Period          312.94795804
		SemiMajorAxis   41.6434
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "GAM Cet C"
{
	ParentBody "GAM Cet"
	Class      "K5 V"
	AppMagn    10.16
	Orbit
	{
		Period          1471872.37
		SemiMajorAxis   16796.1827
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


//Gliese 84;spanish wiki,9thCSB
//spectroscopic binary only known period

Star "Gliese 84 A/HIP 9724"
{
	ParentBody "Gliese 84"
	Class      "M2 V"
	Radius     375840
	AppMagn    10.19
	Orbit
	{
		Period          18.6795
		SemiMajorAxis   3.5206
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Gliese 84 B"
{
	ParentBody "Gliese 84"
	Class      "M V" //unknown, most probably a WD or another red dwarf
	Orbit
	{
		Period          18.6795
		SemiMajorAxis   3.5206
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


//Gliese 105;6thCVB,english and spanish wiki


Barycenter "Gliese 105 (AC)"
{
	ParentBody "Gliese 105"
	Orbit
	{
		Period          38595.7
		SemiMajorAxis   278.8296
		Inclination     49  //RA and IN unknown, just aligned
		AscendingNode   13
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Gliese 105 A/LHS 15/HIP 12114/HD 16160"
{
	ParentBody "Gliese 105 (AC)"
	Class      "K3 V"
	Radius     528960
	AppMagn    6.38
	MassSol    0.81
	Orbit
	{
		Period          61
		SemiMajorAxis   0.1129
		Eccentricity    0.67
		Inclination     49
		AscendingNode   13
		ArgOfPericenter 249
		Epoch           2429630.001471
		MeanAnomaly     0
	}
}

Star "Gliese 105 C"
{
	ParentBody "Gliese 105 (AC)"
	Class      "M7 V"
	MassSol    0.082
	Orbit
	{
		Period          61
		SemiMajorAxis   1.1155
		Eccentricity    0.67
		Inclination     49
		AscendingNode   13
		ArgOfPericenter 69
		Epoch           2429630.001471
		MeanAnomaly     0
	}
}

Star "Gliese 105 B/LHS 16/BX Cet"
{
	ParentBody "Gliese 105"
	Class      "M4 V"
	Radius     194880
	AppMagn    12.22
	MassSol    0.27
	Orbit
	{
		Period          38595.7
		SemiMajorAxis   921.1704
		Inclination     49  //RA and IN unknown, just aligned
		AscendingNode   13
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//Mira;already in SE

//MU Cet;spanish wiki
//multiple but with unknown hierarchy

Star "MU Cet A/HD 17094"
{
	ParentBody "MU Cet"
	Class      "F0 IV"
	Radius     1183200
	AppMagn    4.2
	MassSol    1.6
	Orbit
	{
		Period          3.29
		SemiMajorAxis   1.6289
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "MU Cet B"
{
	ParentBody "MU Cet"
	AppMagn    9 		//unknown
	Orbit
	{
		Period          3.29
		SemiMajorAxis   1.6289
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


//Xi1 Cet;6thCVB,spanish wiki


Star "Xi1 Cet A/HIP 10324/HD 13611"
{
	ParentBody "Xi1 Cet"
	Class      "G6 III"
	Radius     1183200
	AppMagn    4.34
	MassSol    4
	Orbit
	{
		Period          4.4989
		SemiMajorAxis   0.1814
		Inclination     106.47
		AscendingNode   71.24
		ArgOfPericenter 0
		Epoch           2434985.5
		MeanAnomaly     0
	}
}

Star "Xi1 Cet B"
{
	ParentBody "Xi1 Cet"
	Class      "A2 V"
	Orbit
	{
		Period          4.4989
		SemiMajorAxis   0.3455
		Inclination     106.47
		AscendingNode   71.24
		ArgOfPericenter 180
		Epoch           2434985.5
		MeanAnomaly     0
	}
}


//Baten Kaitos;spanish wiki
//spect. binary with just the period

Star "Baten Kaitos A/HIP 8645/HD 11353"
{
	ParentBody "Baten Kaitos"
	Class      "K0 III"
	Radius     17400000
	AppMagn    3.74
	MassSol    2.5
	Orbit
	{
		Period          4.5
		SemiMajorAxis   2.3291
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Baten Kaitos B"
{
	ParentBody "Baten Kaitos"
	AppMagn    8 //unknown,SP companion
	Orbit
	{
		Period          4.5
		SemiMajorAxis   2.3291
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star	"Luyten 726-8 A/Gliese 65 A/BL Cet"
{
	ParentBody	   "Luyten 726-8"
	Class		   "M5 V"
	MassSol			0.102
	Radius			97370
	Lum				0.00006
	AppMagn			12.54
	Orbit
	{
		Epoch			2451971.91
		Period			26.5
		SemiMajorAxis	2.725
		Eccentricity	0.62
		AscendingNode	150.5
		ArgOfPericenter	285.3
		Inclination		127.3
		MeanAnomaly		0
	}
}

Star	"Luyten 726-8 B/Gliese 65 B/UV Cet"
{
	ParentBody	   "Luyten 726-8"
	Class		   "M6 V"
	MassSol			0.1
	Radius			97370
	AppMagn			12.99
	Orbit
	{
		Epoch			2451971.91
		Period			26.5
		SemiMajorAxis	2.725
		Eccentricity	0.62
		AscendingNode	150.5
		ArgOfPericenter	105.3
		Inclination		127.3
		MeanAnomaly		0
	}
}

/////////////////////PERSEUS///////////////////////////////

Barycenter	"Algol (AB)"
{
	ParentBody	"Algol"
	MassSol     4.38

	Orbit
	{
		Period			1.8619
		SemiMajorAxis	0.7425	// mass ratio * 2.69
		Eccentricity	0.225
		Inclination		83.98
		AscendingNode	312.26
		ArgOfPericenter	130.29
		MeanAnomaly     0
	}
}

Star	"Algol A/BET Per A/26 Per A/HIP 14576 A/HD 19356 A"
{
	ParentBody	"Algol (AB)"
	Class		"B8V"
	Luminosity  98
	RadSol      4.13
	MassSol     3.59
	Teff        9200
	Age         0.3

	Orbit
	{
		Epoch           2401987.36966
		Period			0.0078505717
		SemiMajorAxis	0.01118	// mass ratio * 0.062
		Eccentricity	0
		Inclination		97.69
		AscendingNode	312.26	// from (AB)-C pair
		ArgOfPericenter 130.29	// from (AB)-C pair
		MeanAnomaly     0
	}
}

Star	"Algol B/BET Per B/26 Per B/HIP 14576 B/HD 19356 B"
{
	ParentBody	"Algol (AB)"
	Class		"K0IV"
	Luminosity  3.4
	RadSol      3.0
	MassSol     0.79
	Teff        4500
	Age         0.3

	Orbit
	{
		Epoch           2401987.36966
		Period			0.0078505717
		SemiMajorAxis	0.05082 // mass ratio * 0.062
		Eccentricity	0
		Inclination		97.69
		AscendingNode	312.26	// from (AB)-C pair
		ArgOfPericenter	310.29	// from (AB)-C pair
		MeanAnomaly     0
	}
}

Star	"Algol C/BET Per C/26 Per C/HIP 14576 C/HD 19356 C"
{
	ParentBody	"Algol"
	Class		"A5V"
	Luminosity  4.1
	RadSol      0.9
	MassSol     1.67
	Teff        8500
	Age         0.3

	Orbit
	{
		Period			1.8619
		SemiMajorAxis	1.9475	// mass ratio * 2.69
		Eccentricity	0.225
		Inclination		83.98
		AscendingNode	312.26
		ArgOfPericenter	310.29
		MeanAnomaly     0
	}
}


//12 Per;6thCVB, spanish wiki
//very good system

Star "12 Per A/HIP 12623/HD 16739"
{
	ParentBody "12 Per"
	Class      "F8 V"
	Radius     1127520
	AppMagn    4.91
	MassSol    1.38
	Orbit
	{
		Period          0.9068
		SemiMajorAxis   0.6099
		Eccentricity    0.6574
		Inclination     128.17
		AscendingNode   49.29
		ArgOfPericenter 269.29
		Epoch           2449111.859648
		MeanAnomaly     0
	}
}

Star "12 Per B"
{
	ParentBody "12 Per"
	Class      "G2 V"
	Radius     960480
	MassSol    1.24
	Orbit
	{
		Period          0.9068
		SemiMajorAxis   0.6788
		Eccentricity    0.6574
		Inclination     128.17
		AscendingNode   49.29
		ArgOfPericenter 89.29
		Epoch           2449111.859648
		MeanAnomaly     0
	}
}


//50 Per;6thCVB,spanish wiki

Barycenter "ADS 2995"
{
	ParentBody "50 Per"
	Orbit
	{
		Period          2349.63
		SemiMajorAxis   142.4007
		Inclination     104
		AscendingNode   160.3
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "50 Per A/V582 Per/HIP 19335/HD 25998"
{
	ParentBody "50 Per"
	Class      "F7 V"
	Radius     904800
	AppMagn    5.5
	MassSol    1.22
	Orbit
	{
		Period          2349.63
		SemiMajorAxis   107.9061
		Inclination     104     //RA and IN unknown, just aligned
		AscendingNode   160.3
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "ADS 2995 A/HD 25893/HIP 19255"
{
	ParentBody "ADS 2995"
	Class      "G V"
	AppMagn    7.3
	Orbit
	{
		Period          590
		SemiMajorAxis   35.0974
		Eccentricity    0.25
		Inclination     104
		AscendingNode   160.3
		ArgOfPericenter 195
		Epoch           2477111.487313
		MeanAnomaly     0
	}
}

Star "ADS 2995 B"
{
	ParentBody "ADS 2995"
	Class      "K V" //unknown, related with absmag
	AppMagn    9.8
	Orbit
	{
		Period          590
		SemiMajorAxis   45.6266
		Eccentricity    0.25
		Inclination     104
		AscendingNode   160.3
		ArgOfPericenter 15
		Epoch           2477111.487313
		MeanAnomaly     0
	}
}


//AG Per;spanish wiki


Barycenter "AG Per (AB)"
{
	ParentBody "AG Per"
	Orbit
	{
		Period          1168
		SemiMajorAxis   67.1459
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "AG Per A/HIP 19201/HD 25833"
{
	ParentBody "AG Per (AB)"
	Class      "B5 V"
	Radius     2088000
	AbsMagn    -0.95
	MassSol    5.35
	Orbit
	{
		Period          0.00555808
		SemiMajorAxis   0.0325
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "AG Per B"
{
	ParentBody "AG Per (AB)"
	Class      "B4 V"
	Radius     1809600
	AbsMagn    0.55
	MassSol    4.89
	Orbit
	{
		Period          0.00555808
		SemiMajorAxis   0.0356
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "AG Per C"
{
	ParentBody "AG Per"
	Class      "B V" //unknown related with Mass
	MassSol    3.47 
	Orbit
	{
		Period          1168
		SemiMajorAxis   198.1482
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


//Atik;spanish wiki


Star "Atik A/HIP 17448/HD 23180"
{
	ParentBody "Atik"
	Class      "B1 III"
	AppMagn    3.83
	MassSol    17
	Orbit
	{
		Period          0.01210959
		SemiMajorAxis   0.04931392
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Atik B"
{
	ParentBody "Atik"
	Class      "B3 V"
	Orbit
	{
		Period          0.01210959
		SemiMajorAxis   0.10479207
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


//EPS Per;english and spanish wiki


Barycenter "EPS Per A"
{
	ParentBody "EPS Per"
	Orbit
	{
		Period          1168
		SemiMajorAxis   205.7083
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "EPS Per Aa/HIP 18532/HD 24760"
{
	ParentBody "EPS Per A"
	Class      "B0 V"
//    Radius     5331360
	AppMagn    2.9
	MassSol    13.5
	Orbit
	{
		Period          0.03854564
		SemiMajorAxis   0.0068
		Eccentricity    0.55
		ArgOfPericenter 105.8
		Epoch           2447767.543       
		MeanAnomaly     0
	}
}

Star "EPS Per Ab"
{
	ParentBody "EPS Per A"
	Class      "G V"
//    Radius     696000
	Orbit
	{
		Period          0.03854564
		SemiMajorAxis   0.0702
		Eccentricity    0.55
		ArgOfPericenter 285.8
		Epoch           2447767.543       
		MeanAnomaly     0
	}
}

Star "EPS Per B"
{
	ParentBody "EPS Per"
	Class      "A2 V"
	AppMagn    7.59
	Orbit
	{
		Period          1168
		SemiMajorAxis   1450.7334
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


//GAM Per;6thCVB,english wiki
//very good system

Star "GAM Per A/HIP 14328/HD 18925"
{
	ParentBody "GAM Per"
	Class      "G9 III"
	AppMagn    2.93
	MassSol    2.7
	Orbit
	{
		Period          14.593
		SemiMajorAxis   4.0686
		Eccentricity    0.786
		Inclination     90.6
		AscendingNode   244.2
		ArgOfPericenter 169.6
		Epoch           2432288.599436
		MeanAnomaly     0
	}
}

Star "GAM Per B"
{
	ParentBody "GAM Per"
	Class      "A2 V"
	AppMagn    4.4
	MassSol    1.65
	Orbit
	{
		Period          14.593
		SemiMajorAxis   6.6577
		Eccentricity    0.786
		Inclination     90.6
		AscendingNode   244.2
		ArgOfPericenter 349.6
		Epoch           2432288.599436
		MeanAnomaly     0
	}
}


//Menchib;spanish, english wiki
//spectroscopic binary, only known period


Star "Menchib A/HIP 18614/HD 24912"
{
	ParentBody "Menchib"
	Class      "O7 III"
	Radius     9744000
	AppMagn    4.06
	MassSol    31
	Orbit
	{
		Period          0.0190411
		SemiMajorAxis   0.14103117
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Menchib B"
{
	ParentBody "Menchib"
	AppMagn    8 //unknown,SP binary
	Orbit
	{
		Period          0.0190411
		SemiMajorAxis   0.14103117
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


//ZET Per;english and spanish wiki
//    Orbit data with new hipparcos reduction

Star "ZET Per A/HIP 18246/HD 24398"
{
	ParentBody "ZET Per"
	Class      "B1 Ib"
	Radius     14616000
	AppMagn    2.85
	MassSol    19
	Orbit
	{
		Period          34189.772
		SemiMajorAxis   450.4683
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "ZET Per B"
{
	ParentBody "ZET Per"
	Class      "B8 V"
	AppMagn    9.16
	MassSol    3.4
	Orbit
	{
		Period          34189.772
		SemiMajorAxis   2517.3231
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


//MU Per;english and spanish wiki


Star "MU Per A/HIP 19812/HD 26630"
{
	ParentBody "MU Per"
	Class      "G0 Ib"
	Radius     36888000
	AppMagn    4.18
	MassSol    5.8
	Orbit
	{
		Period          0.78
		SemiMajorAxis   0.4675
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "MU Per B"
{
	ParentBody "MU Per"
	Class      "B9 V"
	MassSol    2.2
	Orbit
	{
		Period          0.78
		SemiMajorAxis   1.2325
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//NU Per;english and spanish wiki

Star "NU Per A/HIP 17529/HD 23230"
{
	ParentBody "NU Per"
	Class      "F5 II"
	Radius     14616000
	AppMagn    3.78
	MassSol    4.5
	Orbit
	{
		Period          172067.7833
		SemiMajorAxis   992.3034
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "NU Per B"
{
	ParentBody "NU Per"
	Class      "G V" //unknown related with absmag
	Orbit
	{
		Period          172067.7833
		SemiMajorAxis   4465.3653
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//TAU Per;6thCVB, english and spanish wiki

Star "TAU Per A/HIP 13531/HD 17879"
{
	ParentBody "TAU Per"
	Class      "G4 III"
	Radius     9744000
	AppMagn    3.95
	Orbit
	{
		Period          4.15
		SemiMajorAxis   1.8972
		Eccentricity    0.73
		Inclination     95
		AscendingNode   101
		ArgOfPericenter 54.6
		Epoch           2442974.125204
		MeanAnomaly     0
	}
}

Star "TAU Per B"
{
	ParentBody "TAU Per"
	Class      "A4 V"
	Radius     1531200
	AppMagn    7.5
	Orbit
	{
		Period          4.15
		SemiMajorAxis   2.2746
		Eccentricity    0.73
		Inclination     95
		AscendingNode   101
		ArgOfPericenter 234.6
		Epoch           2442974.125204
		MeanAnomaly     0
	}
}


//TET Per;6thCVB,spanish wiki
//very good system


Star "TET Per A/HIP 12777/HD 16895"
{
	ParentBody "TET Per"
	Class      "F7 V"
	Radius     904800
	AppMagn    4.16
	MassSol    1.25
	Orbit
	{
		Period          2720
		SemiMajorAxis   64.0491
		Eccentricity    0.13
		Inclination     75.44
		AscendingNode   128
		ArgOfPericenter 100.64
		Epoch           2310195.80247
		MeanAnomaly     0
	}
}

Star "TET Per B"
{
	ParentBody "TET Per"
	Class      "M1 V"
	AppMagn    10.25
	MassSol    0.43
	Orbit
	{
		Period          2720
		SemiMajorAxis   186.1893
		Eccentricity    0.13
		Inclination     75.44
		AscendingNode   128
		ArgOfPericenter 280.64
		Epoch           2310195.80247
		MeanAnomaly     0
	}
}


//V606 Per;spanish wiki


Star "V505 Per A/HIP 10961/HD 14384"
{
	ParentBody "V505 Per"
	Class      "F5 V"
	Radius     895752
	AbsMagn    3.66
	MassSol    1.259
	Orbit
	{
		Period          0.0052
		SemiMajorAxis   0.0199
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "V505 Per B"
{
	ParentBody "V505 Per"
	Class      "F5 V"
	Radius     881136
	AbsMagn    3.77
	MassSol    1.251
	Orbit
	{
		Period          0.0052
		SemiMajorAxis   0.0201
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//XY Per;spanish wiki

Star "XY Per A/HIP 17890/HD 275877"
{
	ParentBody "XY Per"
	Class      "A2 V"  //pre main sequence star
	AppMagn    9.44
	MassSol    2.8
	Orbit
	{
		Period          4521.872
		SemiMajorAxis   192.0884
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "XY Per B"
{
	ParentBody "XY Per"
	Class      "A3 V"
	Orbit
	{
		Period          4521.872
		SemiMajorAxis   268.9238
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


///////////////////////CEPHEUS///////////////////////////

//14 Cep;spanish wiki

Star "14 Cep A/HIP 108772/HD 209481"
{
	ParentBody "14 Cep"
	Class      "O9 V"
	Radius     10440000
	AppMagn    5.55
	MassSol    30.4
	Orbit
	{
		Period          0.0084
		SemiMajorAxis   0.0813
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "14 Cep B"
{
	ParentBody "14 Cep"
	Class      "O V"
	Radius     10440000 //unknown
	Orbit
	{
		Period          0.0084
		SemiMajorAxis   0.0813
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//Kurhah;6thCVV,english and spanish wiki
//very good system

Barycenter "Kurhah A"
{
	ParentBody "Kurhah"
	Orbit
	{
		Period          3800
		SemiMajorAxis   105.312
		Eccentricity    0.24
		Inclination     109
		AscendingNode   85
		ArgOfPericenter 114
		Epoch           2360233.983703
		MeanAnomaly     0
	}
}

Star "Kurhah Aa/HIP 108917/HD 209790"
{
	ParentBody "Kurhah A"
	Class      "A3 V"
	Radius     1392000
	AppMagn    4.29
	MassSol    1.7
	Orbit
	{
		Period          2.2452
		SemiMajorAxis   0.9581
		Eccentricity    0.483
		Inclination     70.9
		AscendingNode   89.8
		ArgOfPericenter 272.6
		Epoch           2440948.528493
		MeanAnomaly     0
	}
}

Star "Kurhah Ab"
{
	ParentBody "Kurhah A"
	Class      "F7 V"
	Radius     974400
	AppMagn    6.3
	MassSol    1.2
	Orbit
	{
		Period          2.2452
		SemiMajorAxis   1.3573
		Eccentricity    0.483
		Inclination     70.9
		AscendingNode   89.8
		ArgOfPericenter 92.6
		Epoch           2440948.528493
		MeanAnomaly     0
	}
}

Star "Kurhah B"
{
	ParentBody "Kurhah"
	Class      "F7 V"
	AppMagn    6.4
	MassSol    1.2
	Orbit
	{
		Period          3800
		SemiMajorAxis   254.504
		Eccentricity    0.24
		Inclination     109
		AscendingNode   85
		ArgOfPericenter 294
		Epoch           2360233.983703
		MeanAnomaly     0
	}
}

//Alfirk;6thCVB,english and spanish wiki


Barycenter "Alfirk A"
{
	ParentBody "Alfirk"
	Orbit
	{
		Period          30000
		SemiMajorAxis   391.3043
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Alfirk Aa/HIP 106032/HD 205021"
{
	ParentBody "Alfirk A"
	Class      "B2 III"
	Radius     6264000
	AppMagn    3.2
	MassSol    12
	Orbit
	{
		Period          83
		SemiMajorAxis   9.1122
		Eccentricity    0.732
		Inclination     87.3
		AscendingNode   46.4
		ArgOfPericenter 194.6
		Epoch           2450810.396579
		MeanAnomaly     0
	}
}

Star "Alfirk Ab"
{
	ParentBody "Alfirk A"
	Class      "B V"  //unknown related with     AbsMagn
	AppMagn    6.6
	Orbit
	{
		Period          83
		SemiMajorAxis   32.1608
		Eccentricity    0.732
		Inclination     87.3
		AscendingNode   46.4
		ArgOfPericenter 14.6
		Epoch           2450810.396579
		MeanAnomaly     0
	}
}

Star "Alfirk B"
{
	ParentBody "Alfirk"
	Class      "A V" //unknown related with     AbsMagn
	AppMagn    8
	Orbit
	{
		Period          30000
		SemiMajorAxis   2008.6957
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//CQ Cep;spanish wiki


Star "CQ Cep A"
{
	ParentBody "CQ Cep"
	Class      "WN6"
	Radius     5707200
	AppMagn    8.87
	MassSol    20.8
	Orbit
	{
		Period          0.0045
		SemiMajorAxis   0.0481
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "CQ Cep B"
{
	ParentBody "CQ Cep"
	Class      "O9 Ib"
	Radius     5728080
	MassSol    21.4
	Orbit
	{
		Period          0.0045
		SemiMajorAxis   0.0468
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


//DEL Cep;english and spanish wiki

Star "DEL Cep A/Cepheidus Prototypus/HIP 110991/HD 213306"
{
	ParentBody "DEL Cep"
	Class      "F5 Ib"
	Radius     30972000
	AppMagn    4.07
	MassSol    4.5
	Orbit
	{
		Period          406879.0469
		SemiMajorAxis   5179.3493
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "DEL Cep B/HD 213317"
{
	ParentBody "DEL Cep"
	Class      "B7 V" 
	AppMagn    7.5
	Orbit
	{
		Period          406879.0469
		SemiMajorAxis   5976.1722
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//DH Cep;spanish wiki


Star "DH Cep A/HIP 112470/HD 215835"
{
	ParentBody "DH Cep"
	Class      "O6 V"
	Radius     8978400
	AppMagn    8.61
	MassSol    21.3
	Orbit
	{
		Period          0.0058
		SemiMajorAxis   0.065
		Eccentricity    0.13
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "DH Cep B"
{
	ParentBody "DH Cep"
	Class      "O6 V"
	Radius     7864800
	MassSol    21.3
	Orbit
	{
		Period          0.0058
		SemiMajorAxis   0.065
		Eccentricity    0.13
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


//EK Cep;spanish wiki


Star "EK Cep A/HIP 107083/HD 206821"
{
	ParentBody "EK Cep"
	Class      "A1 V"
	Radius     1099680
	AppMagn    7.88
	MassSol    2.02
	Orbit
	{
		Period          0.0121
		SemiMajorAxis   0.0276
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "EK Cep B"
{
	ParentBody "EK Cep"
	Class      "A V"
	Radius     918720 
	MassSol    1.12
	Orbit
	{
		Period          0.0121
		SemiMajorAxis   0.0497
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//Errai;with exoplanet

//G 251-43;spanish wiki

//WD STAR PRESENT; BINARY WD SYSTEM MOVED TO WD CATALOG

//GP Cep;spanish wiki
//quadruple;unknown data for the other 2 components besides the     Orbital period


Star "GP Cep A/HIP 110154/HD 211853"
{
	ParentBody "GP Cep"
	Class      "WN6"
	AppMagn    9.03
	MassSol    15
	Orbit
	{
		Period          0.0183
		SemiMajorAxis   0.1449
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "GP Cep B"
{
	ParentBody "GP Cep"
	Class      "O6 Ia"
	MassSol    24
	Orbit
	{
		Period          0.0183
		SemiMajorAxis   0.0906
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//BD+85 63;WD STAR PRESENT

//Gliese 909;6thCVB, spanish wiki

Barycenter "Gliese 909 A"
{
	ParentBody "Gliese 909"
	Orbit
	{
		Period          290
		SemiMajorAxis   9.0381
		Eccentricity    0.55
		Inclination     49.58
		AscendingNode   93.91
		ArgOfPericenter 134.14
		Epoch           2457023.16638
		MeanAnomaly     0
	}
}

Star "Gliese 909 Aa/HIP 117712/HD 233778"
{
	ParentBody "Gliese 909 A"
	Class      "K3 V"
	Radius     577680
	AppMagn    6.39
	MassSol    0.73
	Orbit
	{
		Period          0.0212411
		SemiMajorAxis   0.0435
		Inclination     49.58
		AscendingNode   93.91
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Gliese 909 Ab"
{
	ParentBody "Gliese 909 A"
	Class      "K3 V"
	Radius     577680
	MassSol    0.73
	Orbit
	{
		Period          0.0212411
		SemiMajorAxis   0.0435
		Inclination     49.58
		AscendingNode   93.91
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "Gliese 909 B"
{
	ParentBody "Gliese 909"
	Class      "M2 V"
	AppMagn    11.5
	MassSol    0.37
	Orbit
	{
		Period          290
		SemiMajorAxis   35.6638
		Eccentricity    0.55
		Inclination     49.58
		AscendingNode   93.91
		ArgOfPericenter 314.14
		Epoch           2457023.16638
		MeanAnomaly     0
	}
}

//Kruger 60;6thCVB,english and spanish wiki
//very good system

Star "Kruger 60 A/LHS 3814/HIP 110893/HD 239960"
{
	ParentBody "Kruger 60"
	Class      "M3 V"
	Radius     243600
	AppMagn    9.93
	MassSol    0.271
	Orbit
	{
		Period          44.67
		SemiMajorAxis   3.7934
		Eccentricity    0.41
		Inclination     167.2
		AscendingNode   154.5
		ArgOfPericenter 211
		Epoch           2440667.620718
		MeanAnomaly     0
	}
}

Star "Kruger 60 B/LHS 3815/HD 239960 B"
{
	ParentBody "Kruger 60"
	Class      "M4 V"
	Radius     167040
	AppMagn    11.41
	MassSol    0.18
	Orbit
	{
		Period          44.67
		SemiMajorAxis   5.841
		Eccentricity    0.41
		Inclination     167.2
		AscendingNode   154.5
		ArgOfPericenter 31
		Epoch           2440667.620718
		MeanAnomaly     0
	}
}

//VV Cep;6thCVB,english and spanish wiki
//very good system

Star "VV Cep A/HIP 108317/HD 208816"
{
	ParentBody "VV Cep"
	Class      "M2 Ia"
	Radius     1322400000
	AppMagn    5.4
	MassSol    40
	Orbit
	{
		Period          20.34
		SemiMajorAxis   9.2218
		Eccentricity    0.5
		Inclination     90.65
		AscendingNode   310.6
		ArgOfPericenter 122
		Epoch           2433720.714098
		MeanAnomaly     0
	}
}

Star "VV Cep B"
{
	ParentBody "VV Cep"
	Class      "B0 V"
	Radius     4176000
	Orbit
	{
		Period          20.34
		SemiMajorAxis   21.6984
		Eccentricity    0.5
		Inclination     90.65
		AscendingNode   310.6
		ArgOfPericenter 302
		Epoch           2433720.714098
		MeanAnomaly     0
	}
}

//WX Cep;spanish wiki

Star "WX Cep A/HIP 111166/HD 213631"
{
	ParentBody "WX Cep"
	Class      "A3 V"
	Radius     2784000
	AbsMagn    0.21
	MassSol    2.53
	Orbit
	{
		Period          0.0093
		SemiMajorAxis   0.0357
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "WX Cep B"
{
	ParentBody "WX Cep"
	Class      "A V"
	Radius     1879200
	AbsMagn    0.74
	MassSol    2.32
	Orbit
	{
		Period          0.0093
		SemiMajorAxis   0.0389
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//XX Cep;spanish wiki


Star "XX Cep A/HIP 116648/HD 222217"
{
	ParentBody "XX Cep"
	Class      "A4 V"
	Radius     1440720
	AbsMagn    1.73
	MassSol    1.92
	Orbit
	{
		Period          53.5
		SemiMajorAxis   0.0534
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "XX Cep B"
{
	ParentBody "XX Cep"
	Class      "K V"
	Radius     1614720
	AbsMagn    4.43
	MassSol    0.33
	Orbit
	{
		Period          53.5
		SemiMajorAxis   0.3107
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

////////////////////////////MONOCEROS//////////////////////////////

//3 Mon;english, spanish wiki


Star "3 Mon A/HIP 28574/HD 40967"
{
	ParentBody "3 Mon"
	Class      "B5 III"
	Radius     3132000
	AppMagn    4.94
	MassSol    5.85
	Orbit
	{
		Period          3587.6902
		SemiMajorAxis   128.2888
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "3 Mon B"
{
	ParentBody "3 Mon"
	Class      "A V" //unknown, related with     AppMagn
	AppMagn    8.25
	MassSol    2.2
	Orbit
	{
		Period          3587.6902
		SemiMajorAxis   341.1315
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//BET Mon;spanish and english wiki


Barycenter "BET Mon BC"
{
	ParentBody "BET Mon"
	Orbit
	{
		Period          14427.83
		SemiMajorAxis   579.3073
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "BET Mon A/HIP 30867/HD 45726"
{
	ParentBody "BET Mon"
	Class      "B3 V"
	AppMagn    4.6
	MassSol    7
	Orbit
	{
		Period          14427.83
		SemiMajorAxis   1009.6498
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "BET Mon B"
{
	ParentBody "BET Mon BC"
	Class      "B3 V"
	AppMagn    5.4
	MassSol    6.2
	Orbit
	{
		Period          4212.707019
		SemiMajorAxis   295.6854
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "BET Mon C"
{
	ParentBody "BET Mon BC"
	Class      "B3 V"
	AppMagn    5.6
	MassSol    6
	Orbit
	{
		Period          4212.707
		SemiMajorAxis   305.5416
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


//DD Mon;spanish wiki
//close binary


Star "DD Mon A/HD 292319"
{
	ParentBody "DD Mon"
	Class      "G0 V"
	Radius     1148400
	AppMagn    11.1
	MassSol    1.29
	Orbit
	{
		Period          0.0015
		SemiMajorAxis   0.0068
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "DD Mon B"
{
	ParentBody "DD Mon"
	Class      "G V"
	Radius     849120
	MassSol    0.87
	Orbit
	{
		Period          0.0015
		SemiMajorAxis   0.0102
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//EPS Mon;english and spanish wiki


Barycenter "EPS Mon A"
{
	ParentBody "EPS Mon"
	Orbit
	{
		Period          5857.05
		SemiMajorAxis   171.2329
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "EPS Mon Aa/HIP 30419/HD 44769"
{
	ParentBody "EPS Mon A"
	Class      "A5 IV"
	Radius     1531200
	AppMagn    4.41
	MassSol    1.9
	Orbit
	{
		Period          0.90684932
		SemiMajorAxis   0.2612
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "EPS Mon Ab"
{
	ParentBody "EPS Mon A"
	Class      "M V" //unknown, related with mass, could be also a WD
	MassSol    0.5
	Orbit
	{
		Period          0.90684932
		SemiMajorAxis   0.9925
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "EPS Mon B"
{
	ParentBody "EPS Mon"
	Class      "F5 V"
	Radius     835200
	AppMagn    6.6
	MassSol    1.25
	Orbit
	{
		Period          5857.05
		SemiMajorAxis   328.7671
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


//Gliese 250;english and spanish wiki


Star "Gliese 250 A/HIP 32984/HD 50281"
{
	ParentBody "Gliese 250"
	Class      "K3 V"
	Radius     542880
	AppMagn    6.57
	MassSol    0.8
	Orbit
	{
		Period          9969.9178
		SemiMajorAxis   194.337
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Gliese 250 B"
{
	ParentBody "Gliese 250"
	Class      "M2 V"
	Radius     341040
	AppMagn    10.08
	MassSol    0.5
	Orbit
	{
		Period          9969.9178
		SemiMajorAxis   310.9391
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//HD 46375;with exoplanet

//Plaskett's Star;english and spanish wiki

Star "Plaskett's Star A/HIP 31646/HD 47129"
{
	ParentBody "Plaskett's Star"
	Class      "O8 III"
	Radius     8491200
	AppMagn    6.06
	MassSol    54
	Orbit
	{
		Period          0.0394
		SemiMajorAxis   0.2545
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Plaskett's Star B"
{
	ParentBody "Plaskett's Star"
	Class      "O7 III"
	Radius     7516800
	MassSol    56
	Orbit
	{
		Period          0.0394
		SemiMajorAxis   0.2455
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//R Mon;spanish wiki
//pre main sequence stars

Star "R Mon A"
{
	ParentBody "R Mon"
	Class      "A3 V"
	AppMagn    11
	MassSol    10
	Orbit
	{
		Period          9.0403
		SemiMajorAxis   65.2174
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "R Mon B"
{
	ParentBody "R Mon"
	Class      "F V"
	MassSol    1.5
	Orbit
	{
		Period          9.0403
		SemiMajorAxis   434.7826
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//Ross 614;6thCVB,english and spanish wiki
//very good system


Star "Ross 614 A/GJ 214 A/LHS 1849/V577 Mon/HIP 30920"
{
	ParentBody "Ross 614"
	Class      "M4 V"
	Radius     174000
	AppMagn    11.3
	MassSol    0.2228
	Orbit
	{
		Period          16.1342
		SemiMajorAxis   1.4147
		Eccentricity    0.371
		Inclination     51.8
		AscendingNode   30.7
		ArgOfPericenter 223
		Epoch           2451318
		MeanAnomaly     0
	}
}

Star "Ross 614 B/GJ 214 B/LHS 1850"
{
	ParentBody "Ross 614"
	Class      "M8 V"
	Radius     90480
	AppMagn    14.8
	MassSol    0.11
	Orbit
	{
		Period          16.1342
		SemiMajorAxis   2.8474
		Eccentricity    0.371
		Inclination     51.8
		AscendingNode   30.7
		ArgOfPericenter 43
		Epoch           2451318
		MeanAnomaly     0
	}
}

//S Mon;6thCVB,english, spanish wiki

Star "S Mon A/HIP 31978/HD 47839"
{
	ParentBody "S Mon"
	Class      "O7 V"
	AppMagn    4.66
	MassSol    30
	Orbit
	{
		Period          74.28
		SemiMajorAxis   27.5656
		Eccentricity    0.716
		Inclination     51.2
		AscendingNode   52.6
		ArgOfPericenter 69.2
		Epoch           2450105.479135
		MeanAnomaly     0
	}
}

Star "S Mon B"
{
	ParentBody "S Mon"
	Class      "O9 V"
	AppMagn    5.9
	MassSol    20
	Orbit
	{
		Period          74.28
		SemiMajorAxis   41.3485
		Eccentricity    0.716
		Inclination     51.2
		AscendingNode   52.6
		ArgOfPericenter 249.2
		Epoch           2450105.479135
		MeanAnomaly     0
	}
}


//U Mon;spanish wiki

Star "U Mon A/HIP 36521/HD 59693"
{
	ParentBody "U Mon"
	Class      "K0 Ib"
	AppMagn    7.45
	MassSol    6.5 //mean mass
	Orbit
	{
		Period          7.1151
		SemiMajorAxis   1551.7562
		Eccentricity    0.43
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "U Mon B"
{
	ParentBody "U Mon"
	AppMagn    16 //unknown
	Orbit
	{
		Period          7.1151
		SemiMajorAxis   1551.7562
		Eccentricity    0.43
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


//V789 Mon;spanish wiki


Star "V789 Mon A"
{
	ParentBody "V789 Mon"
	Class      "K5 V"
	Radius     549840
	AppMagn    9.34
	MassSol    0.56
	Orbit
	{
		Period          0.0038
		SemiMajorAxis   0.0121
		Eccentricity    0.0129
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "V789 Mon B"
{
	ParentBody "V789 Mon"
	Class      "K6 V"
	Radius     487200
	MassSol    0.52
	Orbit
	{
		Period          0.0038
		SemiMajorAxis   0.013
		Eccentricity    0.0129
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

////////////////////////////PEGASUS////////////////////////////////


//1 Peg;spanish wiki
//weak system data

Barycenter "1 Peg B"
{
	ParentBody "1 Peg"
	Orbit
	{
		Period          36707.25
		SemiMajorAxis   911.4812
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "1 Peg A/HIP 105502/HD 203504"
{
	ParentBody "1 Peg"
	Class      "K1 III"
	Radius     8352000
	AppMagn    4.09
	Orbit
	{
		Period          36707.25
		SemiMajorAxis   811.2182
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//1 Peg;B component, spectroscopic binary with only known period

Star "1 Peg Ba"
{
	ParentBody "1 Peg B"
	Class      "K0 V"
	AppMagn    8.4
	Orbit
	{
		Period          3.04
		SemiMajorAxis   1.2732 //unknown
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "1 Peg Bb"
{
	ParentBody "1 Peg B"
	AppMagn    18 //unknown,SP companion
	Orbit
	{
		Period          3.04
		SemiMajorAxis   1.2732 //unknown
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//85 Peg;6thCVB, english and spanish wiki


Barycenter "85 Peg B"
{
	ParentBody "85 Peg"
	Orbit
	{
		Period          26.28
		SemiMajorAxis   5.8922
		Eccentricity    0.38
		Inclination     49
		AscendingNode   290
		ArgOfPericenter 96
		Epoch           2447672.966091
		MeanAnomaly     0
	}
}

Star "85 Peg A/HIP 171/HD 224930"
{
	ParentBody "85 Peg"
	Class      "G5 V"
	Radius     633360
	AppMagn    5.75
	MassSol    0.88
	Orbit
	{
		Period          26.28
		SemiMajorAxis   4.4191
		Eccentricity    0.38
		Inclination     49
		AscendingNode   290
		ArgOfPericenter 276
		Epoch           2447672.966091
		MeanAnomaly     0
	}
}

Star "85 Peg Ba"
{
	ParentBody "85 Peg B"
	Class      "K7 V"
	Radius     466320
	AppMagn    8.89
	MassSol    0.55
	Orbit
	{
		Period          3.475013
		SemiMajorAxis   0.3333
		Inclination     49 //RA and IN unknown just aligned
		AscendingNode   290
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "85 Peg Bb"
{
	ParentBody "85 Peg B"
	Class      "M V"
	MassSol    0.11
	Orbit
	{
		Period          3.475
		SemiMajorAxis   1.6667
		Inclination     49 //RA and IN unknown just aligned
		AscendingNode   290
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//EQ Peg;6thCVB,english and spanish wiki
//very good system


Star "EQ Peg A/HIP 116132"
{
	ParentBody "EQ Peg"
	Class      "M3 V"
	Radius     264480
	AppMagn    10.52
	MassSol    0.34
	Orbit
	{
		Period          359
		SemiMajorAxis   13.622
		Eccentricity    0.2
		Inclination     123.5
		AscendingNode   82.1
		ArgOfPericenter 354
		Epoch           2454466.470988
		MeanAnomaly     0
	}
}

Star "EQ Peg B"
{
	ParentBody "EQ Peg"
	Class      "M4 V"
	Radius     160080
	AppMagn    12.4
	MassSol    0.16
	Orbit
	{
		Period          359
		SemiMajorAxis   28.9467
		Eccentricity    0.2
		Inclination     123.5
		AscendingNode   82.1
		ArgOfPericenter 174
		Epoch           2454466.470988
		MeanAnomaly     0
	}
}

//Matar; english and spanish wiki


Star "Matar A/HIP 112158/HD 215182"
{
	ParentBody "Matar"
	Class      "G2 III"
	Radius     12528000
	AppMagn    2.94
	MassSol    3.82
	Orbit
	{
		Period          2.2274
		SemiMajorAxis   1.0126
		Eccentricity    0.183
		ArgOfPericenter 344.7
		Epoch           2452025
		MeanAnomaly     0
	}
}

Star "Matar B"
{
	ParentBody "Matar"
	Class      "A5 V"
	Orbit
	{
		Period          2.2274
		SemiMajorAxis   2.0359
		Eccentricity    0.183
		ArgOfPericenter 164.7
		Epoch           2452025
		MeanAnomaly     0
	}
}

//GAM Peg;spanish and english wiki

Star "GAM Peg A/HIP 1067/HD 886"
{
	ParentBody "GAM Peg"
	Class      "B2 IV"
	Radius     3132000
	AppMagn    3.83
	MassSol    7
	Orbit
	{
		Period          0.0187
		SemiMajorAxis   0.0406
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "GAM Peg B"
{
	ParentBody "GAM Peg"
	Class      "B V" //unknown, remaining mass system of 2.6
	Orbit       //could be also an A     Class      star
	{
		Period          0.0187
		SemiMajorAxis   0.1094
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//II Peg;english and spanish wiki


Star "II Peg A/HIP 117915/HD 224085"
{
	ParentBody "II Peg"
	Class      "K2 IV"
	Radius     2366400
	AppMagn    7.4
	MassSol    0.8
	Orbit
	{
		Period          0.0184
		SemiMajorAxis   0.0107
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "II Peg B"
{
	ParentBody "II Peg"
	Class      "M2 V"
	MassSol    0.4
	Orbit
	{
		Period          0.0184
		SemiMajorAxis   0.0213
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//IK Peg;WD STAR PRESENT

//IOT Peg;6thCVB,english and spanish wiki
//very good system

Star "IOT Peg A/HIP 109176/HD 210027"
{
	ParentBody "IOT Peg"
	Class      "F5 V"
	Radius     974400
	AppMagn    3.81
	MassSol    1.32
	Orbit
	{
		Period          0.028
		SemiMajorAxis   0.0458
		Eccentricity    0.001764
		Inclination     95.83
		AscendingNode   176.262
		ArgOfPericenter 272.8
		Epoch           2452997.378
		MeanAnomaly     0
	}
}

Star "IOT Peg B"
{
	ParentBody "IOT Peg"
	Class      "G8 V"
	MassSol    0.8
	Orbit
	{
		Period          0.028
		SemiMajorAxis   0.0756
		Eccentricity    0.001764
		Inclination     95.83
		AscendingNode   176.262
		ArgOfPericenter 92.8
		Epoch           2452997.378
		MeanAnomaly     0
	}
}

//KAP Peg;6thCVB, english and spanish wiki


Barycenter "KAP Peg B"
{
	ParentBody "KAP Peg"
	Orbit
	{
		Period          11.5747
		SemiMajorAxis   3.1955
		Eccentricity    0.314
		Inclination     107.911
		AscendingNode   289.037
		ArgOfPericenter 124.666
		Epoch           2452401.52
		MeanAnomaly     0
	}
}

Star "KAP Peg A/HIP 107354/HD 206901"
{
	ParentBody "KAP Peg"
	Class      "F5 IV"
	AppMagn    4.94
	MassSol    1.55
	Orbit
	{
		Period          11.57468493
		SemiMajorAxis   5.0922
		Eccentricity    0.314
		Inclination     107.911
		AscendingNode   289.037
		ArgOfPericenter 304.666
		Epoch           2452401.52
		MeanAnomaly     0
	}
}

Star "KAP Peg Ba"
{
	ParentBody "KAP Peg B"
	Class      "A V"
	AppMagn    5.04
	MassSol    1.66
	Orbit
	{
		Period          0.01636027
		SemiMajorAxis   0.0292
		Eccentricity    0.0073
		Inclination     107.911
		AscendingNode   289.037
		ArgOfPericenter 179
		Epoch           2452402.22
		MeanAnomaly     0
	}
}

Star "KAP Peg Bb"
{
	ParentBody "KAP Peg B"
	Class      "K V"
	MassSol    0.81
	Orbit
	{
		Period          0.0164
		SemiMajorAxis   0.0597
		Eccentricity    0.0073
		Inclination     107.911
		AscendingNode   289.037
		ArgOfPericenter 359
		Epoch           2452402.22
		MeanAnomaly     0
	}
}

//OO Peg;english and spanish wiki


Star "OO Peg A/HIP 107099/HD 206417"
{
	ParentBody "OO Peg"
	Class      "A2 V"
	Radius     1524240
	AppMagn    8.26
	MassSol    1.72
	Orbit
	{
		Period          0.0082
		SemiMajorAxis   0.0303
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "OO Peg B"
{
	ParentBody "OO Peg"
	Class      "A2 V"
	Radius     953520 
	MassSol    1.69
	Orbit
	{
		Period          0.0082
		SemiMajorAxis   0.0308
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//PSI Peg;spanish wiki

Star "PSI Peg A/HIP 118131/HD 224427"
{
	ParentBody "PSI Peg"
	Class      "M3 III"
	Radius     84216000
	AppMagn    4.67
	MassSol    2.8 //unknown, mass corresponding around 2/3 of the system
	Orbit
	{
		Period          55.6
		SemiMajorAxis   5.7895
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "PSI Peg B"
{
	ParentBody "PSI Peg"
	Class      "G V" //unknown, mass corresponding around 1/3 of the system
	Orbit
	{
		Period          55.6
		SemiMajorAxis   16.2105
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//U Peg;spanish wiki
//contact binary


Star "U Peg A/HIP 118149"
{
	ParentBody "U Peg"
	Class      "G2 V"
	Radius     835200
	AppMagn    9.23
	MassSol    1.15
	Orbit
	{
		Period          0.001
		SemiMajorAxis   0.003
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "U Peg B"
{
	ParentBody "U Peg"
	Class      "G2 V"
	Radius     508080
	MassSol    0.38
	Orbit
	{
		Period          0.001
		SemiMajorAxis   0.009
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//KSI Peg;spanish wiki


Star "KSI Peg A/LHS 3851/GJ 872 A/Gliese 872 A/HIP 112447/HD 215648"
{
	ParentBody "KSI Peg"
	Class      "F7 V"
	Radius     1329360
	AppMagn    4.2
	MassSol    1.2
	Orbit
	{
		Period          1886.4
		SemiMajorAxis   45.5417
		ArgOfPericenter 0
 
		MeanAnomaly     0
	}
}

Star "KSI Peg B/LHS 3852/GJ 872 B/Gliese 872 B"
{
	ParentBody "KSI Peg"
	Class      "M1 V"
	AppMagn    11.7
	MassSol    0.41
	Orbit
	{
		Period          1886.4
		SemiMajorAxis   133.2927
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//Homam;english and spanish wiki


Star "Homam A/HIP 112029/HD 214923"
{
	ParentBody "Homam"
	Class      "B8 V"
	Radius     2784000
	AppMagn    3.4
	MassSol    3.3
	Orbit
	{
		Period          604913.2352
		SemiMajorAxis   1985.8206
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Homam B"
{
	ParentBody "Homam"
	Class      "K6 V"
	AppMagn    11
	Orbit
	{
		Period          604913.2352
		SemiMajorAxis   9361.7255
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//Gliese 829
//General:spanish wiki
//Spectra for B comp:www.solstation.com/stars3/100-ms.htm
//Semimajor axis:Coronagraphic Survey for Companions of Stars within 8 pc
//authors:B. R. Oppenheimer,D. A. Golimowski,S. R. Kulkarni, K. Matthews,
//T. Nakajima, M. Creech-Eakman and S. T. Durrance

Star "Gliese 829 A/HIP 106106"
{
	ParentBody "Gliese 829"
	Class      "M3 V"
	AppMagn    10.35
	MassSol    0.37
	Orbit
	{
		Period          0.4113
		SemiMajorAxis   0.25
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Gliese 829 B"
{
	ParentBody "Gliese 829"
	Class      "M3 V"
	AppMagn    11.1
	MassSol    0.37
	Orbit
	{
		Period          0.4113
		SemiMajorAxis   0.25
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


//////////////////////////////////ARA//////////////////////////////////////////

//41 G. Ara;6thCVB,english and spanish wiki
//very good system

Star "41 G. Ara A/HIP 84720/HD 156274"
{
	ParentBody "41 G. Ara"
	Class      "G8 V"
	Radius     535920
	AppMagn    5.61
	MassSol    0.9
	Orbit
	{
		Period          953
		SemiMajorAxis   46.0277
		Eccentricity    0.825
		Inclination     40.5
		AscendingNode   137.3
		ArgOfPericenter 329.7
		Epoch           2417759.630011
		MeanAnomaly     0
	}
}

Star "41 G. Ara B"
{
	ParentBody "41 G. Ara"
	Class      "M0 V"
	Radius     334080
	AppMagn    8.88
	Orbit
	{
		Period          953
		SemiMajorAxis   71.4222
		Eccentricity    0.825
		Inclination     40.5
		AscendingNode   137.3
		ArgOfPericenter 149.7
		Epoch           2417759.630011
		MeanAnomaly     0
	}
}

//EPS2 Ara;WD STAR PRESENT

//GAM Ara;english and spanish wiki, prof. jim kaler

Star "GAM Ara A/HIP 85267/HD 157246"
{
	ParentBody "GAM Ara"
	Class      "B1 Ib"
	Radius     10092000
	AppMagn    3.34
	MassSol    12
	Orbit
	{
		Period          135000
		SemiMajorAxis   537.4046
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "GAM Ara B"
{
	ParentBody "GAM Ara"
	Class      "A7 V" 
	AppMagn    10.3
	Orbit
	{
		Period          135000
		SemiMajorAxis   5862.5954
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//R Ara;english and italian wiki
//eclipsing binary with mass transfer

Star "R Ara A/HIP 81589/HD 149730"
{
	ParentBody "R Ara"
	Class      "B9 V"
	AppMagn    6.62
	Orbit
	{
		Period          0.0121
		SemiMajorAxis   0.0311
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "R Ara B"
{
	ParentBody "R Ara"
	Class      "F V" //unknown, related with appmag
	AppMagn    8.2
	Orbit
	{
		Period          0.0121
		SemiMajorAxis   0.0551
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//V870 Ara;spanish wiki
//contact binary

Star "V870 Ara A/HIP 88853/HD 165235"
{
	ParentBody "V870 Ara"
	Class      "F8 V"
	Radius     1176240
	AppMagn    9
	MassSol    1.503
	Orbit
	{
		Period          0.00109529
		SemiMajorAxis   0.00094463
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "V870 Ara B"
{
	ParentBody "V870 Ara"
	Class      "F8 V"
	Radius     424560
	MassSol    0.123
	Orbit
	{
		Period          0.00109529
		SemiMajorAxis   0.01154296
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

////////////////////////////AQUILA/////////////////////////////////



Star "Alshain A/HIP 98036/HD 188512"
{
	ParentBody "Alshain"
	Class      "G8 IV"
	Radius     2088000
	AppMagn    3.71
	MassSol    1.3
	Orbit
	{
		Period          1865.6332
		SemiMajorAxis   36.0877
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Alshain B"
{
	ParentBody "Alshain"
	Class      "M3 V"
	AppMagn    11.4
	MassSol    0.33
	Orbit
	{
		Period          1865.6332
		SemiMajorAxis   142.1638
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


//CHI Aql;english, spanish wiki, prof. jim kaler


Star "CHI Aql A/HIP 96967/HD 186203"
{
	ParentBody "CHI Aql"
	Class      "G2 II"
	AppMagn    5.8
	MassSol    5
	Orbit
	{
		Period          890.3951
		SemiMajorAxis   69.375
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "CHI Aql B"
{
	ParentBody "CHI Aql"
	Class      "B5 V"
	AppMagn    6.68
	MassSol    3
	Orbit
	{
		Period          890.3951
		SemiMajorAxis   115.625
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


//DEL Aql;6thCVB,english, spanish wiki
//very good system


Star "DEL Aql A/HIP 95501/HD 182640"
{
	ParentBody "DEL Aql"
	Class      "F0 IV"
	Radius     1419840
	AppMagn    3.4
	MassSol    1.65
	Orbit
	{
		Period          3.4282
		SemiMajorAxis   0.2496
		Eccentricity    0.36
		Inclination     150
		AscendingNode   337
		ArgOfPericenter 191
		Epoch           2434955.5
		MeanAnomaly     0
	}
}

Star "DEL Aql B"
{
	ParentBody "DEL Aql"
	Class      "K V"
	Radius     424560
	MassSol    0.67
	Orbit
	{
		Period          3.4282
		SemiMajorAxis   0.6148
		Eccentricity    0.36
		Inclination     150
		AscendingNode   337
		ArgOfPericenter 11
		Epoch           2434955.5
		MeanAnomaly     0
	}
}

//EPS Aql;WD STAR PRESENT

//FF Aql;english and spanish wiki

Star "FF Aql A/HIP 93124/HD 176155"
{
	ParentBody "FF Aql"
	Class      "F6 Ib"
	Radius     12528000
	AppMagn    5.31
	MassSol    4.5
	Orbit
	{
		Period          3.9244
		SemiMajorAxis   1.1917
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "FF Aql B"
{
	ParentBody "FF Aql"
	Class      "F1 V"
	MassSol    1.6
	Orbit
	{
		Period          3.9244
		SemiMajorAxis   3.3517
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//OO Aql;spanish wiki
//contact binary


Star "OO Aql A/HD 187183"
{
	ParentBody "OO Aql"
	Class      "F9 V"
	Radius     967440
	AppMagn    9.2
	MassSol    1.07
	Orbit
	{
		Period          0.00138849
		SemiMajorAxis   0.0071269
		ArgOfPericenter 0
 
		MeanAnomaly     0
	}
}

Star "OO Aql B"
{
	ParentBody "OO Aql"
	Class      "F9 V"
	Radius     897840
	MassSol    0.9
	Orbit
	{
		Period          0.00138849
		SemiMajorAxis   0.0084731
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


//SIG Aql;english and spanish wiki
//close binary


Star "SIG Aql A/HIP 96665/HD 185507"
{
	ParentBody "SIG Aql"
	Class      "B3 V"
	Radius     2937120
	AppMagn    5.17
	MassSol    6.8
	Orbit
	{
		Period          0.00534318
		SemiMajorAxis   0.03112482
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "SIG Aql B"
{
	ParentBody "SIG Aql"
	Class      "B3 V"
	Radius     2122800
	MassSol    5.4
	Orbit
	{
		Period          0.00534318
		SemiMajorAxis   0.03919422
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//TET Aql;6thCVB,english and spanish wiki


Star "TET Aql A/HIP 99473/HD 191692"
{
	ParentBody "TET Aql"
	Class      "B9 III"
	Radius     3340800
	AppMagn    3.23
	MassSol    3.6
	Orbit
	{
		Period          0.0469
		SemiMajorAxis   0.1253
		Eccentricity    0.6
		Inclination     143.5
		AscendingNode   99
		ArgOfPericenter 215
		Epoch           2447801.7
		MeanAnomaly     0
	}
}

Star "TET Aql B"
{
	ParentBody "TET Aql"
	Class      "B9 III"
	Radius     1670400
	MassSol    2.9
	Orbit
	{
		Period          0.0469
		SemiMajorAxis   0.1555
		Eccentricity    0.6
		Inclination     143.5
		AscendingNode   99
		ArgOfPericenter 35
		Epoch           2447801.7
		MeanAnomaly     0
	}
}

//U Aql;english and spanish wiki
//spectroscopic binary
//cepheid

Star "U Aql A/HIP 95820/HD 183344"
{
	ParentBody "U Aql"
	Class      "G1 II"
	Radius     19870800 //medium
	AppMagn    6.61
	MassSol    5.9
	Orbit
	{
		Period          5.08493151
		SemiMajorAxis   116.48402792
		Eccentricity    0.17
		Inclination     74
		AscendingNode   190
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "U Aql B"
{
	ParentBody "U Aql"
	Class      "B9 V"
	Orbit
	{
		Period          5.08493151
		SemiMajorAxis   245.44848741
		Eccentricity    0.17
		Inclination     74
		AscendingNode   190
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//V805 Aql;english, spanish wiki


Star "V805 Aql A/HIP 93809/HD 177708"
{
	ParentBody "V805 Aql"
	Class      "A2 V"
	Radius     1468560
	AppMagn    7.58
	MassSol    2.11
	Orbit
	{
		Period          0.00659781
		SemiMajorAxis   0.02378444
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "V805 Aql B"
{
	ParentBody "V805 Aql"
	Class      "A9 V"
	Radius     1218000
	MassSol    1.63
	Orbit
	{
		Period          0.00659781
		SemiMajorAxis   0.03078844
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


//V1285 Aql;spanish wiki


Star "V1285 Aql A/HIP 92871"
{
	ParentBody "V1285 Aql"
	Class      "M3 V"
	Radius     306240
	AppMagn    10.18
	MassSol    0.32
	Orbit
	{
		Period          0.0008742
		SemiMajorAxis   0.00376946
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "V1285 Aql B"
{
	ParentBody "V1285 Aql"
	Class      "M3 V"
	Radius     306240
	MassSol    0.3
	Orbit
	{
		Period          0.0008742
		SemiMajorAxis   0.00402075
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//ZET Aql;spanish and english wiki


Star "ZET Aql A/HIP 93747/HD 177724"
{
	ParentBody "ZET Aql"
	Class      "A0 V"
	Radius     1579920
	AppMagn    2.983
	MassSol    2.37
	Orbit
	{
		Period          1257.9108
		SemiMajorAxis   29.8486
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "ZET Aql B"
{
	ParentBody "ZET Aql"
	Class      "M V"
	AbsMagn    12
	Orbit
	{
		Period          1257.9108
		SemiMajorAxis   136.0409
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}



////////////////////////CAMELOPARDALIS/////////////////////////////


//7 Cam;6thCVB,spanish and english wiki


Barycenter "7 Cam (AB)"
{
	ParentBody "7 Cam"
	Orbit
	{
		Period          58613.4043
		SemiMajorAxis   367.1327
		Inclination     106.5    //unknown,just aligned with AB
		AscendingNode   150.2
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Barycenter "7 Cam A"
{
	ParentBody "7 Cam (AB)"
	Orbit
	{
		Period          2733
		SemiMajorAxis   241.0061
		Eccentricity    0.436
		Inclination     106.5
		AscendingNode   150.2
		ArgOfPericenter 119.6
		Epoch           2472728.580927
		MeanAnomaly     0
	}
}



Star "7 Cam Aa/HIP 23040/HD 31278"
{
	ParentBody "7 Cam A"
	Class      "A1 V"
	Radius     1322400
	AppMagn    4.45
	MassSol    3.9
	Orbit
	{
		Period          0.0106
		SemiMajorAxis   0.011
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "7 Cam Ab"
{
	ParentBody "7 Cam A"
	Class      "M V" //unknown, related with mass, could be also a white dwarf
	MassSol    0.62
	Orbit
	{
		Period          0.0106
		SemiMajorAxis   0.0691
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "7 Cam B"
{
	ParentBody "7 Cam (AB)"
	Class      "F V" //unknown, related with     AppMagn
	AppMagn    7.9
	Orbit
	{
		Period          2733
		SemiMajorAxis   84.2455
		Eccentricity    0.436
		Inclination     106.5
		AscendingNode   150.2
		ArgOfPericenter 299.6
		Epoch           2472728.580927
		MeanAnomaly     0
	}
}

Star "7 Cam C"
{
	ParentBody "7 Cam"
	Class      "K V" //unknown, related with     AppMagn
	AppMagn    11.3
	Orbit
	{
		Period          58613.4043
		SemiMajorAxis   2516.3029
		Inclination     106.5   //unknown,just aligned with AB
		AscendingNode   150.2
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}


//16 Cam; english and spanish wiki


Star "16 Cam A/HIP 25197/HD 34787"
{
	ParentBody "16 Cam"
	Class      "A0 V"
	Radius     2296800
	AppMagn    5.25
	MassSol    2.5
	Orbit
	{
		Period          87.421
		SemiMajorAxis   8.5399
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "16 Cam B"
{
	ParentBody "16 Cam"
	Class      "G V" //unknown,     AppMagn        Class
	AppMagn    9.55
	Orbit
	{
		Period          87.421
		SemiMajorAxis   21.3497
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//53 Cam;6thCVB english and spanish wiki



Star "53 Cam A/HIP 39261/HD 65339"
{
	ParentBody "53 Cam"
	Class      "A2 V"
	Radius     1642560
	AppMagn    6
	MassSol    2.07
	Orbit
	{
		Period          6.6504
		SemiMajorAxis   2.7078
		Eccentricity    0.706
		Inclination     55.4
		AscendingNode   118.3
		ArgOfPericenter 8.3
		Epoch           2451993.087342
		MeanAnomaly     0
	}
}

Star "53 Cam B"
{
	ParentBody "53 Cam"
	AppMagn    12 //unknown
	Orbit
	{
		Period          6.6504
		SemiMajorAxis   2.7078
		Eccentricity    0.706
		Inclination     55.4
		AscendingNode   118.3
		ArgOfPericenter 188.3
		Epoch           2451993.087342
		MeanAnomaly     0
	}
}

//BD+62 597;WD STAR PRESENT

//BET Cam;english and spanish wiki

Star "BET Cam A/HIP 23522/HD 31920"
{
	ParentBody "BET Cam"
	Class      "G1 Ib"
	Radius     45240000
	AppMagn    4.03
	MassSol    7
	Orbit
	{
		Period          1362905.9546
		SemiMajorAxis   5435.3071
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "BET Cam B"
{
	ParentBody "BET Cam"
	Class      "A5 V"
	Orbit
	{
		Period          1362905.9546
		SemiMajorAxis   20024.8156
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


//BK Cam;spanish wiki


Star "BK Cam A/HIP 15520/HD 20336"
{
	ParentBody "BK Cam"
	Class      "B2 V"
	Radius     3132000
	AppMagn    4.73
	MassSol    7.5
	Orbit
	{
		Period          4.5031
		SemiMajorAxis   3.3607
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "BK Cam B"
{
	ParentBody "BK Cam"
	Class      "B V"
	Orbit
	{
		Period          4.5031
		SemiMajorAxis   3.3607
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


//CS Cam;english and spanish wiki


Star "CS Cam A/HIP 16228/HD 21291"
{
	ParentBody "CS Cam"
	Class      "B9 Ia"
	Radius     5568000
	AppMagn    4.26
	MassSol    12
	Orbit
	{
		Period          12611.3039
		SemiMajorAxis   382.7768
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "CS Cam B"
{
	ParentBody "CS Cam"
	Class      "B V" //unknown, related with     AppMagn
	AppMagn    8.26
	Orbit
	{
		Period          12611.3039
		SemiMajorAxis   998.5483
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//Stein 2051;WD STAR PRESENT

//31 Cam;english and spanish wiki


Star "31 Cam A/HIP 27971/HD 39220"
{
	ParentBody "31 Cam"
	Class      "A2 V"
	Radius     1461600
	AppMagn    5.197
	MassSol    3.1
	Orbit
	{
		Period          0.008
		SemiMajorAxis   0.0368
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "31 Cam B"
{
	ParentBody "31 Cam"
	AppMagn    10 //unknown
	Orbit
	{
		Period          0.008
		SemiMajorAxis   0.0368
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//Y Cam;spanish wiki


Star "Y Cam A/HIP 37440"
{
	ParentBody "Y Cam"
	Class      "A7 V"
	Radius     2032320
	AppMagn    10.5
	MassSol    1.7
	Orbit
	{
		Period          0.0091
		SemiMajorAxis   0.0106
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Y Cam B"
{
	ParentBody "Y Cam"
	Class      "K1 IV"
	Radius     2053200
	MassSol    0.4
	Orbit
	{
		Period          0.0091
		SemiMajorAxis   0.045
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//Z Cam;WD STAR PRESENT

/////////////////////////////////////EQUULEUS//////////////////////////////////////////

//Kitalpha;6thCVB,english and spanish wiki
//very good system

Star "Kitalpha A/HIP 104987/HD 202447"
{
	ParentBody "Kitalpha"
	Class      "G0 III"
	AppMagn    3.92
	MassSol    2.1
	Orbit
	{
		Period          0.2707
		SemiMajorAxis   0.3249
		Eccentricity    0.0056
		Inclination     151.5
		AscendingNode   33.9
		ArgOfPericenter 270
		Epoch           2447592.1
		MeanAnomaly     0
	}
}

Star "Kitalpha B"
{
	ParentBody "Kitalpha"
	Class      "A5 V"
	MassSol    1.9
	Orbit
	{
		Period          0.2707
		SemiMajorAxis   0.3591
		Eccentricity    0.0056
		Inclination     151.5
		AscendingNode   33.9
		ArgOfPericenter 90
		Epoch           2447592.1
		MeanAnomaly     0
	}
}


//DEL Equ;6thCVB,english and spanish wiki
//very good system


Star "DEL Equ A/HIP 104858/HD 202275"
{
	ParentBody "DEL Equ"
	Class      "F5 V"
	AppMagn    5.19
	MassSol    1.59
	Orbit
	{
		Period          5.7097
		SemiMajorAxis   2.1806
		Eccentricity    0.436851
		Inclination     99.4083
		AscendingNode   23.362
		ArgOfPericenter 7.735
		Epoch           2453112.071
		MeanAnomaly     0
	}
}

Star "DEL Equ B"
{
	ParentBody "DEL Equ"
	Class      "G0 V"
	AppMagn    5.52
	MassSol    1.66
	Orbit
	{
		Period          5.7097
		SemiMajorAxis   2.0887
		Eccentricity    0.436851
		Inclination     99.4083
		AscendingNode   23.362
		ArgOfPericenter 187.735
		Epoch           2453112.071
		MeanAnomaly     0
	}
}


//EPS Equ;6thCVB,english and spanish wiki


Barycenter "EPS Equ (ABC)"
{
	ParentBody "EPS Equ"
	Orbit
	{
		Period          155824.5622
		SemiMajorAxis   563.9344
		Inclination     110.9
		AscendingNode   253.9
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Barycenter "EPS Equ (AB)"
{
	ParentBody "EPS Equ (ABC)"
	Orbit
	{
		Period          9108.65
		SemiMajorAxis   447.5778
		Inclination     110.9
		AscendingNode   253.9
		ArgOfPericenter 360
		Epoch           2415020.31352
		MeanAnomaly     0
	}
}



Star "EPS Equ A/HIP 103569/HD 199766"
{
	ParentBody "EPS Equ (AB)"
	Class      "F5 IV"
	Radius     1809600
	AppMagn    5.96
	MassSol    1.6
	Orbit
	{
		Period          101.485
		SemiMajorAxis   19.1919
		Eccentricity    0.705
		Inclination     92.17
		AscendingNode   105.15
		ArgOfPericenter 340.19
		Epoch           2422460.297109
		MeanAnomaly     0
	}
}

Star "EPS Equ B"
{
	ParentBody "EPS Equ (AB)"
	Class      "F7 IV"
	Radius     1600800
	AppMagn    6.31
	MassSol    1.55
	Orbit
	{
		Period          101.485
		SemiMajorAxis   19.811
		Eccentricity    0.705
		Inclination     92.17
		AscendingNode   105.15
		ArgOfPericenter 160.19
		Epoch           2422460.297109
		MeanAnomaly     0
	}
}

Star "EPS Equ C/BD+03 4473 C/HIP 103571"
{
	ParentBody "EPS Equ (ABC)"
	Class      "G0 V"
	AppMagn    7.3
	Orbit
	{
		Period          9108.65
		SemiMajorAxis   154.8761
		Inclination     110.9
		AscendingNode   253.9
		ArgOfPericenter 180
		Epoch           2415020.31352
		MeanAnomaly     0
	}
}

Star "EPS Equ D/BD+03 4473 D"
{
	ParentBody "EPS Equ"
	Class      "K V" //unknown, related with     AppMagn
	AppMagn    12.4
	Orbit
	{
		Period          155824.5622
		SemiMajorAxis   3736.0656
		Inclination     110.9   //unkwnown, just aligned
		AscendingNode   253.9
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}


//GAM Equ;english and spanish wiki

Star "GAM Equ A/HIP 104521/HD 201601"
{
	ParentBody "GAM Equ"
	Class      "A9 V"
	Radius     1461600
	AppMagn    4.71
	MassSol    1.8
	Orbit
	{
		Period          250
		SemiMajorAxis   17.8662
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "GAM Equ B"
{
	ParentBody "GAM Equ"
	Class      "K V" //unknown related with     AppMagn
	AppMagn    8.2
	Orbit
	{
		Period          250
		SemiMajorAxis   36.1338
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}



//////////////////////////////////SAGGITA////////////////////////////////////////////




Star "DEL Sge A/HIP 97365/HD 187076"
{
	ParentBody "DEL Sge"
	Class      "M2 II"
	Radius     87000000
	AppMagn    3.68
	MassSol    3.8
	Orbit
	{
		Period          10.2
		SemiMajorAxis   3.809
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "DEL Sge B"
{
	ParentBody "DEL Sge"
	Class      "A0 V"
	Radius     1809600
	MassSol    2.9
	Orbit
	{
		Period          10.2
		SemiMajorAxis   4.991
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


//Gliese 745;6thCVB,spanish wiki
//very good system


Star "Gliese 745 A/Ross 730/LHS 3432/HIP 93873"
{
	ParentBody "Gliese 745"
	Class      "M1 VI"
	Radius     231072
	AppMagn    10.95
	MassSol    0.348
	Orbit
	{
		Period          36000
		SemiMajorAxis   511.0309
		Eccentricity    0.64
		Inclination     153
		AscendingNode   206
		ArgOfPericenter 231
		Epoch           4735769.244574
		MeanAnomaly     0
	}
}

Star "Gliese 745 B/Ross 731/LHS 3433/HIP 93899"
{
	ParentBody "Gliese 745"
	Class      "M1 VI"
	Radius     235944
	AppMagn    10.99
	MassSol    0.35
	Orbit
	{
		Period          36000
		SemiMajorAxis   505.2237
		Eccentricity    0.64
		Inclination     153
		AscendingNode   206
		ArgOfPericenter 51
		Epoch           4735769.244574
		MeanAnomaly     0
	}
}

//U Sge;spanish wiki
//detached binary


Star "U Sge A/HIP 94910/HD 181182"
{
	ParentBody "U Sge"
	Class      "B7 IV"
	Radius     2860560
	AppMagn    6.51
	MassSol    5.45
	Orbit
	{
		Period          0.009262
		SemiMajorAxis   0.023016
		Eccentricity    0.03
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "U Sge B"
{
	ParentBody "U Sge"
	Class      "G2 III"
	Radius     3925440
	AppMagn    9.13
	MassSol    1.99
	Orbit
	{
		Period          0.009262
		SemiMajorAxis   0.063033
		Eccentricity    0.03
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//VZ Sge;english and spanish wiki


Star "VZ Sge A/HD 189577"
{
	ParentBody "VZ Sge"
	Class      "M4 III"
	Radius     105792000
	AppMagn    5.31
	Orbit
	{
		Period          313234.2442
		SemiMajorAxis   1829.3328
		Eccentricity    0.03
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "VZ Sge B"
{
	ParentBody "VZ Sge"
	Class      "G V" //unknown, related with     AppMagn
	AppMagn    11.5
	Orbit
	{
		Period          313234.2442
		SemiMajorAxis   5487.9985
		Eccentricity    0.03
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


//ZET Sge;6thCVB,spanish wiki


Barycenter "ZET Sge (AB)"
{
	ParentBody "ZET Sge"
	Orbit
	{
		Period          7832.91
		SemiMajorAxis   187.5
		Inclination     132.33
		AscendingNode   340.97
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "ZET Sge A/HIP 97496/HD 187362"
{
	ParentBody "ZET Sge (AB)"
	Class      "A3 V"
	Radius     1670400
	AppMagn    5.6
	MassSol    2.1
	Orbit
	{
		Period          23.25452055
		SemiMajorAxis   5.1912
		Eccentricity    0.7948
		Inclination     132.33
		AscendingNode   340.97
		ArgOfPericenter 355.3
		Epoch           2444199.6
		MeanAnomaly     0
	}
}

Star "ZET Sge B"
{
	ParentBody "ZET Sge (AB)"
	Class      "A4 V"
	Radius     1392000
	AppMagn    6
	MassSol    2
	Orbit
	{
		Period          23.25452055
		SemiMajorAxis   5.4508
		Eccentricity    0.7948
		Inclination     132.33
		AscendingNode   340.97
		ArgOfPericenter 175.3
		Epoch           2444199.6
		MeanAnomaly     0
	}
}

Star "ZET Sge C"
{
	ParentBody "ZET Sge"
	Class      "F5 V"
	Orbit
	{
		Period          7832.91
		SemiMajorAxis   512.5
		Inclination     132.33
		AscendingNode   340.97
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

///////////////////////////SCUTUM////////////////////////////////////////////

//2M 1845-1409;english,SIMBAD coordinates

Star "2MASS J18450079-1409036"
{
	ParentBody "2M 1845-1409 AB"
	Class      "M5 V"
	Orbit
	{
		Period          57.11         //generic
		SemiMajorAxis   9.9225 //only known separation
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "2MASS J18450097-1409053"
{
	ParentBody "2M 1845-1409 AB"
	Class      "M5 V" 
	Orbit
	{
		Period          51.11        //generic
		SemiMajorAxis   9.9225 //only known separation
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//BET Sct;6thCVB,spanish wiki



Star "BET Sct A/HIP 92175/HD 173764"
{
	ParentBody "BET Sct"
	Class      "G4 II"
	Radius     58464000
	AppMagn    4.23
	MassSol    6
	Orbit
	{
		Period          2.2849
		SemiMajorAxis   0.9605 //changed
		Eccentricity    0.35
		Inclination     105.9
		AscendingNode   288.1
		ArgOfPericenter 33.9
		Epoch           2422480.9
		MeanAnomaly     0
	}
}

Star "BET Sct B"
{
	ParentBody "BET Sct"
	Class      "A0 V"
	MassSol    2.3
	Orbit
	{
		Period          2.2849
		SemiMajorAxis   2.5056 //changed
		Eccentricity    0.35
		Inclination     105.9
		AscendingNode   288.1
		ArgOfPericenter 213.9
		Epoch           2422480.9
		MeanAnomaly     0
	}
}

//DEL Sct;english, spanish wiki, prof jim kaler website


Barycenter "DEL Sct (AB)"
{
	ParentBody "DEL Sct"
	Orbit
	{
		Period          96498.85
		SemiMajorAxis   827.4769
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "DEL Sct A/HIP 91726/HD 172748"
{
	ParentBody "DEL Sct (AB)"
	Class      "F2 III"
	AppMagn    4.72
	MassSol    2.23
	Orbit
	{
		Period          17089.41005578
		SemiMajorAxis   204.8916
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "DEL Sct B"
{
	ParentBody "DEL Sct (AB)"
	Class      "K V"
	AppMagn    12.2
	Orbit
	{
		Period          17089.41005578
		SemiMajorAxis   736.9489  
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "DEL Sct C"
{
	ParentBody "DEL Sct"
	Class      "G V"
	AppMagn    9.2
	Orbit
	{
		Period          96498.85
		SemiMajorAxis   2456.5721
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//ZET Sct;6thCVB,english wiki
//astrometic binary

Star "ZET Sct A/HIP 90135/HD 169156"
{
	ParentBody "ZET Sct"
	Class      "G9 III"
	AppMagn    4.67
	Orbit
	{
		Period          6.5033
		SemiMajorAxis   3.16 //changed
		Eccentricity    0.1
		Inclination     89
		AscendingNode   226
		ArgOfPericenter 242.1
		Epoch           2418278.3
		MeanAnomaly     0
	}
}

Star "ZET Sct B"
{
	ParentBody "ZET Sct"
	AppMagn    9 //unknown
	Orbit
	{
		Period          6.5033
		SemiMajorAxis   3.16  //changed
		Eccentricity    0.1
		Inclination     89
		AscendingNode   226
		ArgOfPericenter 62.1
		Epoch           2418278.3
		MeanAnomaly     0
	}
}


/////////////////////////SERPENS/////////////////////////////////////

//5 Ser;spanish wiki

Star "5 Ser A/HIP 74975/HD 136202"
{
	ParentBody "5 Ser"
	Class      "F8 III"
	AppMagn    5.1
	MassSol    1.25
	Orbit
	{
		Period          3392.8919
		SemiMajorAxis   109.7903
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "5 Ser B/LHS 3060"
{
	ParentBody "5 Ser"
	Class      "K4 V"
	AppMagn    10.1
	Orbit
	{
		Period          3392.8919
		SemiMajorAxis   175.9459
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


//39 Ser;6thCVB,spanish wiki
//changed semiaxis according confirmed period, spect binary in 6thCVB


Star "39 Ser A/HIP 77801/HD 142267"
{
	ParentBody "39 Ser"
	Class      "G0 V"
	Radius     765600
	AppMagn    6.1
	MassSol    0.88
	Orbit
	{
		Period          0.3797
		SemiMajorAxis   0.1898
		Eccentricity    0.5
		Inclination     84.8
		AscendingNode   41.6
		ArgOfPericenter 286.4
		Epoch           2444165.4
		MeanAnomaly     0
	}
}

Star "39 Ser B"
{
	ParentBody "39 Ser"
	Class      "M V" //invisible low mas star, could be also a WD
	Orbit
	{
		Period          0.3797
		SemiMajorAxis   0.334
		Eccentricity    0.5
		Inclination     84.8
		AscendingNode   41.6
		ArgOfPericenter 106.4
		Epoch           2444165.4
		MeanAnomaly     0
	}
}


//BET Ser;spanish wiki


Barycenter "BET Ser (AB)"
{
	ParentBody "BET Ser"
	Orbit
	{
		Period          465701.86
		SemiMajorAxis   1799.2424
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "BET Ser A/HIP 77233/HD 141003"
{
	ParentBody "BET Ser (AB)"
	Class      "A2 IV"
	AppMagn    3.65
	MassSol    2.4
	Orbit
	{
		Period          32364.42064183
		SemiMajorAxis   378.5047
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "BET Ser B"
{
	ParentBody "BET Ser (AB)"
	Class      "K3 V"
	AppMagn    9.9
	Orbit
	{
		Period          32364.42064183
		SemiMajorAxis   1121.4953
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "BET Ser C"
{
	ParentBody "BET Ser"
	Class      "K V"
	AppMagn    10.7
	Orbit
	{
		Period          465701.86
		SemiMajorAxis   7700.7576
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


//DEL Ser;6thCVB, english and spanish wiki


Barycenter "DEL Ser (AB)"
{
	ParentBody "DEL Ser"
	Orbit
	{
		Period          118091.6145
		SemiMajorAxis   785.60949587
		Inclination     111    //unknown IN and RA, just aligned
		AscendingNode   171
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Barycenter "DEL Ser (CD)"
{
	ParentBody "DEL Ser"
	Orbit
	{
		Period          118091.6145
		SemiMajorAxis   3465.92424647
		Inclination     111     //unknown IN and RA, just aligned
		AscendingNode   171
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}



Star "DEL Ser A/HIP 76276/HD 138917"
{
	ParentBody "DEL Ser (AB)"
	Class      "F0 IV"
	Radius     3480000
	AppMagn    4.2
	MassSol    2.4
	Orbit
	{
		Period          1038
		SemiMajorAxis   115.13496933
		Eccentricity    0.16
		Inclination     111
		AscendingNode   171
		ArgOfPericenter 110
		Epoch           2557464.771045
		MeanAnomaly     0
	}
}

Star "DEL Ser B"
{
	ParentBody "DEL Ser (AB)"
	Class      "F0 IV"
	Radius     2088000
	AppMagn    5.2
	MassSol    2.1
	Orbit
	{
		Period          1038
		SemiMajorAxis   131.58282209
		Eccentricity    0.16
		Inclination     111
		AscendingNode   171
		ArgOfPericenter 290
		Epoch           2557464.771045
		MeanAnomaly     0
	}
}

Star "DEL Ser C"
{
	ParentBody "DEL Ser (CD)"
	Class      "M V"
	AbsMagn    14
	Orbit
	{
		Period          4728.8074
		SemiMajorAxis   138.93901119
		Inclination     111     //unknown IN and RA, just aligned
		AscendingNode   171
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "DEL Ser D"
{
	ParentBody "DEL Ser (CD)"
	Class      "M V"
	AbsMagn    15
	Orbit
	{
		Period          4728.8074
		SemiMajorAxis   144.49657163
		Inclination     111   //unknown IN and RA, just aligned
		AscendingNode   171
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//Xi Ser;english and spanish wiki


Barycenter "XI Ser A"
{
	ParentBody "XI Ser"
	Orbit
	{
		Period          11535.63
		SemiMajorAxis   153.6669
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "XI Ser Aa/HIP 86263/HD 159876"
{
	ParentBody "XI Ser A"
	Class      "F0 III"
	Radius     2575200
	AppMagn    354
	MassSol    2
	Orbit
	{
		Period          0.00627397
		SemiMajorAxis   0.0186
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "XI Ser Ab"
{
	ParentBody "XI Ser A"
	Class      "F V" //unknown, related with remaining mass system
	Orbit       //3.18 - 2 = 1.18
	{       //could be even a heavy WD
		Period          0.00627397
		SemiMajorAxis   0.0314
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "XI Ser B"
{
	ParentBody "XI Ser"
	Class      "M V"
	AbsMagn    13
	Orbit
	{
		Period          11535.63
		SemiMajorAxis   651.5478
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//MS Ser;spanish wiki


Star "MS Ser A/HIP 78259/HD 143313"
{
	ParentBody "MS Ser"
	Class      "K2 IV"
	Radius     2436000
	AppMagn    8.21
	MassSol    0.71
	Orbit
	{
		Period          0.0247
		SemiMajorAxis   0.0241
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "MS Ser B"
{
	ParentBody "MS Ser"
	Class      "G8 V"
	Radius     696000
	MassSol    0.86
	Orbit
	{
		Period          0.0247
		SemiMajorAxis   0.0199
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//PSI Ser;english and spanish wiki


Star "PSI Ser A/HIP 77052/HD 140538"
{
	ParentBody "PSI Ser"
	Class      "G2 V"
	Radius     682080
	AppMagn    5.95
	MassSol    0.98
	Orbit
	{
		Period          528.79
		SemiMajorAxis   25.0033
		Eccentricity    0.146
		Inclination     144.5
		AscendingNode   210.7
		ArgOfPericenter 129.5
		Epoch           2429542.343344
		MeanAnomaly     0
	}
}

Star "PSI Ser B"
{
	ParentBody "PSI Ser"
	Class      "M V" //unknown, related with     AppMagn
	AbsMagn    12
	Orbit
	{
		Period          528.79
		SemiMajorAxis   49.0066
		Eccentricity    0.146
		Inclination     144.5
		AscendingNode   210.7
		ArgOfPericenter 309.5
		Epoch           2429542.343344
		MeanAnomaly     0
	}
}

//TAU7 Ser;english and spanish wiki


Star "TAU7 Ser A/HIP 76878/HD 140232"
{
	ParentBody "TAU7 Ser"
	Class      "A2 V"
	Radius     1266720
	AppMagn    5.8
	MassSol    1.7
	Orbit
	{
		Period          901.5544
		SemiMajorAxis   31.2286
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "TAU7 Ser B"
{
	ParentBody "TAU7 Ser"
	Class      "M V"
	AbsMagn    13.1
	MassSol    0.58
	Orbit
	{
		Period          901.5544
		SemiMajorAxis   91.5321
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//TET Ser;spanish, english wiki


Star "TET1 Ser/HIP 92951/HD 175639/HR 7142"
{
	ParentBody "TET Ser"
	Class      "A5 V"
	Radius     1392000
	AppMagn    4.62
	MassSol    1.9
	Orbit
	{
		Period          13650.447
		SemiMajorAxis   445.3988
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "TET2 Ser/HIP 92946/HD 175638/HR 7141"
{
	ParentBody "TET Ser"
	Class      "A5 V"
	Radius     1392000
	AppMagn    4.98
	MassSol    1.9
	Orbit
	{
		Period          13650.447
		SemiMajorAxis   445.3988
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

////////////////////////////TRIANGULUM///////////////////////////

//Metallah;english and spanish wiki

Star "Metallah Aa/HIP 8793/HD 11443"
{
	ParentBody "Metallah"
	Class      "F6 IV"
	Radius     2241120
	AppMagn    3.42
	MassSol    1.7
	Orbit
	{
		Period          0.0048
		SemiMajorAxis   0.0021
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Metallah Ab"
{
	ParentBody "Metallah"
	Class      "M V" //unknown, could be also a WD
	MassSol    0.11
	Orbit
	{
		Period          0.0048
		SemiMajorAxis   0.0324
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//BET Tri;6thCVB,english and spanish wiki

Star "BET Tri A/HIP 10064/HD 13161"
{
	ParentBody "BET Tri"
	Class      "A5 IV"
	Radius     2784000
	AppMagn    3
	MassSol    3.5
	Orbit
	{
		Period          0.086
		SemiMajorAxis   0.0694
		Eccentricity    0.44
		Inclination     129.9
		AscendingNode   64.9
		ArgOfPericenter 298.1
		Epoch           2447729.07
		MeanAnomaly     0
	}
}

Star "BET Tri B"
{
	ParentBody "BET Tri"
	Class      "G V"
	Orbit
	{
		Period          0.086
		SemiMajorAxis   0.243
		Eccentricity    0.44
		Inclination     129.9
		AscendingNode   64.9
		ArgOfPericenter 118.1
		Epoch           2447729.07
		MeanAnomaly     0
	}
}

Star	"DEL Tri A/8 Tri A"
{
	ParentBody	"DEL Tri"
	Class		"G0V"
	AbsMagn		4.70107
	Age			8.5     // Wikipedia estimate
	MassSol		1.09
	Radius		684962	// Randomized Guess; not 100% certain

	Orbit
	{
		Period			0.0274335	// 10.0201 days
		SemiMajorAxis	0.0454326	// mass ratio 1.09:0.75; mutual sep of 0.11146 au derived from mass
		Eccentricity	0.0107427
		Inclination		56.714
		AscendingNode	122.736
		ArgOfPericenter	28.474
		MeanAnomaly		7.270
	}
}

Star	"DEL Tri B/8 Tri B"
{
	ParentBody	"DEL Tri"
	Class		"K4V"
	AbsMagn		6.70107
	Age			8.5     // Wikipedia estimate
	MassSol		0.75
	Radius		518162	// Randomized Guess; not 100% certain

	Orbit
	{
		Period			0.0274335	// 10.0201 days
		SemiMajorAxis	0.0660286	// mass ratio 1.09:0.75; mutual sep of 0.11146 au derived from mass
		Eccentricity	0.0107427
		Inclination		56.714
		AscendingNode	122.736
		ArgOfPericenter	208.474
		MeanAnomaly		7.270
	}
}

//IOT Tri;english and spanish wiki

Barycenter "IOT Tri A"
{
	ParentBody "IOT Tri"
	Orbit
	{
		Period          2300
		SemiMajorAxis   123.96825397
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Barycenter "IOT Tri B"
{
	ParentBody "IOT Tri"
	Orbit
	{
		Period          2300
		SemiMajorAxis   231.03174603
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}



Star "IOT Tri Aa/HIP 10280/HD 13480"
{
	ParentBody "IOT Tri A"
	Class      "G5 III"
	AppMagn    4.94
	Orbit
	{
		Period          0.0404
		SemiMajorAxis   0.05365854
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "IOT Tri Ab"
{
	ParentBody "IOT Tri A"
	Class      "F5 V"
	Orbit
	{
		Period          0.0404
		SemiMajorAxis   0.14634146
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "IOT Tri Ba"
{
	ParentBody "IOT Tri B"
	Class      "F5 V"
	Orbit
	{
		Period          0.0061
		SemiMajorAxis   0.025
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "IOT Tri Bb"
{
	ParentBody "IOT Tri B"
	Class      "F5 V"
	Orbit
	{
		Period          0.0061
		SemiMajorAxis   0.025
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//X Tri;spanish wiki
//eclipsing binary

Star "X Tri A/HIP 9383/HD 12211"
{
	ParentBody "X Tri"
	Class      "A3 V"
	Radius     1190160
	AppMagn    9
	MassSol    2.3
	Orbit
	{
		Period          0.0027
		SemiMajorAxis   0.01
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "X Tri B"
{
	ParentBody "X Tri"
	Class      "F V" //uknown,related with mass
	Radius     1364160
	MassSol    1.2
	Orbit
	{
		Period          0.0027
		SemiMajorAxis   0.0192
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


/////////////////////DELPHINUS//////////////////////////


//13 Del;spanish wiki

Star "13 Del A/HIP 102633/HD 198069"
{
	ParentBody "13 Del"
	Class      "A0 V"
	AppMagn    5.61
	MassSol    2.93
	Orbit
	{
		Period          1423.8846
		SemiMajorAxis   66.8372
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "13 Del B"
{
	ParentBody "13 Del"
	Class      "F V"
	AppMagn    8.51
	Orbit
	{
		Period          1423.8846
		SemiMajorAxis   130.5554
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//Sualocin;6thCVB,english and spanish wiki


Star "Sualocin A/ALF Del A/HIP 101958/HD 196867"
{
	ParentBody "Sualocin"
	Class      "B9 IV"
	Radius     2853600
	AppMagn    3.86
	MassSol    3.7
	Orbit
	{
		Period          17
		SemiMajorAxis   3.9959
		Eccentricity    0.47
		Inclination     160
		AscendingNode   129
		ArgOfPericenter 99
		Epoch           2445627.609778
		MeanAnomaly     0
	}
}

Star "Sualocin B/ALF Del G"
{
	ParentBody "Sualocin"
	Class      "A V"
	AppMagn    6.43
	Orbit
	{
		Period          17
		SemiMajorAxis   7.7814
		Eccentricity    0.47
		Inclination     160
		AscendingNode   129
		ArgOfPericenter 279
		Epoch           2445627.609778
		MeanAnomaly     0
	}
}


//Rotanev;6thCVB,english and spanish wiki


Star "Rotanev A/HIP 101769/HD 196524"
{
	ParentBody "Rotanev"
	Class      "F5 III"
	AppMagn    4.11
	MassSol    1.75
	Orbit
	{
		Period          26.7008
		SemiMajorAxis   5.9362
		Eccentricity    0.35595
		Inclination     61.323
		AscendingNode   357.179
		ArgOfPericenter 168.86
		Epoch           2437961.5
		MeanAnomaly     0
	}
}

Star "Rotanev B"
{
	ParentBody "Rotanev"
	Class      "F5 IV"
	AppMagn    5.02
	MassSol    1.47
	Orbit
	{
		Period          26.7008
		SemiMajorAxis   7.0669
		Eccentricity    0.35595
		Inclination     61.323
		AscendingNode   357.179
		ArgOfPericenter 348.86
		Epoch           2437961.5
		MeanAnomaly     0
	}
}


//GAM Del;6thCVB, english and spanish wiki


Star "GAM2 DEL/GAM Del A/HR 7948/HIP 102532/HD 197964"
{
	ParentBody "GAM Del"
	Class      "K1 IV"
	Radius     5220000
	AppMagn    4.36
	MassSol    1.7
	Orbit
	{
		Period          3249
		SemiMajorAxis   148.4212
		Eccentricity    0.88
		Inclination     148.78
		AscendingNode   88.06
		ArgOfPericenter 331.16
		Epoch           2562943.404026
		MeanAnomaly     0
	}
}

Star "GAM1 DEL/GAM Del B/HR 7947/HIP 102531/HD 197963"
{
	ParentBody "GAM Del"
	Class      "F7 V"
	Radius     1740000
	AppMagn    5.03
	MassSol    1.5
	Orbit
	{
		Period          3249
		SemiMajorAxis   168.2107
		Eccentricity    0.88
		Inclination     148.78
		AscendingNode   88.06
		ArgOfPericenter 151.16
		Epoch           2562943.404026
		MeanAnomaly     0
	}
}

//HD 195019; with exoplanets

//IOT Del;spanish wiki
//spectroscopic binary

Star "IOT Del A/HIP 101800/HD 196544"
{
	ParentBody "IOT Del"
	Class      "A2 V"
	Radius     1113600
	AppMagn    5.42
	MassSol    2.08
	Orbit
	{
		Period          0.0302
		SemiMajorAxis   0.078
		Eccentricity    0.23 //confirmed
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "IOT Del B"
{
	ParentBody "IOT Del"
	AppMagn    11 //unknown,SP companion
	Orbit
	{
		Period          0.0302
		SemiMajorAxis   0.078
		Eccentricity    0.23
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


//KAP Del;6thCVB,spanish wiki, prof. jim kaler


Barycenter "KAP Del A"
{
	ParentBody "KAP Del"
	Orbit
	{
		Period          319437.51
		SemiMajorAxis   1996.4883
		Inclination     107    //unknown, just aligned
		AscendingNode   326
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "KAP Del Aa/HIP 101916/HD 196755"
{
	ParentBody "KAP Del A"
	Class      "G5 IV"
	Radius     2018400
	AppMagn    5.05
	MassSol    1.4
	Orbit
	{
		Period          45
		SemiMajorAxis   3.4738
		Eccentricity    0.8
		Inclination     107
		AscendingNode   326
		ArgOfPericenter 8
		Epoch           2441025.558073
		MeanAnomaly     0
	}
}

Star "KAP Del Ab"
{
	ParentBody "KAP Del A"
	AppMagn    5.05 //unknown
	Orbit
	{
		Period          45
		SemiMajorAxis   12.1581
		Eccentricity    0.8
		Inclination     107
		AscendingNode   326
		ArgOfPericenter 188
		Epoch           2441025.558073
		MeanAnomaly     0
	}
}

Star "KAP Del C"
{
	ParentBody "KAP Del"
	Class      "K3 V"
	AppMagn    8.8
	MassSol    0.81
	Orbit
	{
		Period          319437.51
		SemiMajorAxis   4436.6406
		Inclination     107    //unknown, just aligned
		AscendingNode   326
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//LS Del;spanish wiki
//close binary


Star "LS Del A/HD 199497"
{
	ParentBody "LS Del"
	Class      "F5 V"
	Radius     549840
	AppMagn    8.65
	MassSol    0.47
	Orbit
	{
		Period          0.000996
		SemiMajorAxis   0.008612
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "LS Del B"
{
	ParentBody "LS Del"
	Class      "F V"
	Radius     856080
	MassSol    1.23
	Orbit
	{
		Period          0.000996
		SemiMajorAxis   0.003291
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


//MR Del;spanish wiki


Star "MR Del A/HIP 101236/HD 195434"
{
	ParentBody "MR Del"
	Class      "K0 V"
	Radius     577680
	AppMagn    7.01
	MassSol    0.69
	Orbit
	{
		Period          0.001428
		SemiMajorAxis   0.006639
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "MR Del B"
{
	ParentBody "MR Del"
	Class      "K V"
	Radius     452400
	MassSol    0.63
	Orbit
	{
		Period          0.001428
		SemiMajorAxis   0.007272
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//TY Del;spanish wiki


Star "TY Del A"
{
	ParentBody "TY Del"
	Class      "B9 V"
	Radius     1336320
	AppMagn    9.7
	MassSol    2.8
	Orbit
	{
		Period          0.003535
		SemiMajorAxis   0.008237
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "TY Del B"
{
	ParentBody "TY Del"
	Class      "G0 IV"
	Radius     1433760
	MassSol    0.84
	Orbit
	{
		Period          0.003535
		SemiMajorAxis   0.027455
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//W Del; spanish wiki


Star "W Del A/HIP 101780/HD 352682"
{
	ParentBody "W Del"
	Class      "A0 V"
	Radius     1670400
	AppMagn    9.69
	MassSol    2.5
	Orbit
	{
		Period          0.013158
		SemiMajorAxis   0.013396
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "W Del B"
{
	ParentBody "W Del"
	Class      "G5 IV"
	Radius     2992800
	MassSol    0.5
	Orbit
	{
		Period          0.013158
		SemiMajorAxis   0.06698
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//////////////////////////CORONA BOREALIS/////////////////////////////

//Alphecca;english wiki

Star "Alphecca A/HIP 76267/HD 139006"
{
	ParentBody "Alphecca"
	Class      "A0 V"
	Radius     2011440
	AppMagn    2.24
	MassSol    2.58
	Orbit
	{
		Period          0.047529
		SemiMajorAxis   0.052571
		Eccentricity    0.37
		Inclination     88.2
		ArgOfPericenter 311
		MeanAnomaly     0
	}
}

Star "Alphecca B"
{
	ParentBody "Alphecca"
	Class      "G5 V"
	Radius     626400
	AppMagn    7.1
	MassSol    0.92
	Orbit
	{
		Period          0.047529
		SemiMajorAxis   0.147429
		Eccentricity    0.37
		Inclination     88.2
		ArgOfPericenter 131
		MeanAnomaly     0
	}
}

//Nusakan;6thcVB,english and spanish wiki

Star "Nusakan A/HIP 75695/HD 137909"
{
	ParentBody "Nusakan"
	Class      "A5 V"
	Radius     1830480
	AppMagn    3.68
	MassSol    2.09
	Orbit
	{
		Period          10.5367
		SemiMajorAxis   2.8618
		Eccentricity    0.53971
		Inclination     111.452
		AscendingNode   148.041
		ArgOfPericenter 180.21
		Epoch           2444412.8
		MeanAnomaly     0
	}
}

Star "Nusakan B"
{
	ParentBody "Nusakan"
	Class      "F2 V"
	Radius     1085760
	AppMagn    5.2
	MassSol    1.4
	Orbit
	{
		Period          10.5367
		SemiMajorAxis   4.2722
		Eccentricity    0.53971
		Inclination     111.452
		AscendingNode   148.041
		ArgOfPericenter 0.21
		Epoch           2444412.8
		MeanAnomaly     0
	}
}

//EPS CrB;exoplanet

//ETA CrB;brown dwarf companion

//GAM CrB;6thCVB,spanish wiki


Star "GAM CrB A/HIP 76952/HD 140436"
{
	ParentBody "GAM CrB"
	Class      "B9 IV"
	Radius     1322400
	AppMagn    4.04
	MassSol    2.6
	Orbit
	{
		Period          91.2
		SemiMajorAxis   13.48
		Eccentricity    0.48
		Inclination     94.45
		AscendingNode   111.75
		ArgOfPericenter 103.8
		Epoch           2426561.967001
		MeanAnomaly     0
	}
}

Star "GAM CrB B"
{
	ParentBody "GAM CrB"
	Class      "A3 V"
	Radius     904800
	AppMagn    5.6
	MassSol    1.85
	Orbit
	{
		Period          91.2
		SemiMajorAxis   18.9449
		Eccentricity    0.48
		Inclination     94.45
		AscendingNode   111.75
		ArgOfPericenter 283.8
		Epoch           2426561.967001
		MeanAnomaly     0
	}
}


//IOT CrB;english and spanish wiki


Star "IOT CrB A/HIP 78493/HD 143807"
{
	ParentBody "IOT CrB"
	Class      "A0 V"
	Radius     974400
	AppMagn    4.98
	MassSol    2.3
	Orbit
	{
		Period          0.097112
		SemiMajorAxis   0.05317
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "IOT CrB B"
{
	ParentBody "IOT CrB"
	AppMagn    10 //unknown, SP binary
	Orbit
	{
		Period          0.097112
		SemiMajorAxis   0.24458
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//RW CrB;spanish wiki
//semi detached close binary


Star "RW CrB A/HIP 76648/HD 139815"
{
	ParentBody "RW CrB"
	Class      "F0 V"
	Radius     1071840
	AppMagn    10.22
	MassSol    1.6
	Orbit
	{
		Period          0.001989
		SemiMajorAxis   0.003985
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "RW CrB B"
{
	ParentBody "RW CrB"
	Class      "G8 IV"
	Radius     765600
	MassSol    0.4
	Orbit
	{
		Period          0.001989
		SemiMajorAxis   0.015938
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//SIG CrB;6thCVB,spanish wiki
//4th component rejected


Barycenter "SIG CrB A"
{
	ParentBody "SIG CrB"
	Orbit
	{
		Period          726
		SemiMajorAxis   35.9342
		Eccentricity    0.72
		Inclination     32.3
		AscendingNode   28
		ArgOfPericenter 57.3
		Epoch           2387700.197051
		MeanAnomaly     0
	}
}

Star "SIG CrB Aa/HIP 79607/HD 146361"
{
	ParentBody "SIG CrB A"
	Class      "F9 V"
	Radius     793440
	AppMagn    5.64
	MassSol    1.11
	Orbit
	{
		Period          0.00312115
		SemiMajorAxis   0.0138
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "SIG CrB Ab"
{
	ParentBody "SIG CrB A"
	Class      "G0 V"
	Radius     765600
	MassSol    1.08
	Orbit
	{
		Period          0.00312115
		SemiMajorAxis   0.0142
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "SIG CrB B"
{
	ParentBody "SIG CrB"
	Class      "G1 V"
	AppMagn    6.42
	MassSol    1
	Orbit
	{
		Period          726
		SemiMajorAxis   78.6241
		Eccentricity    0.72
		Inclination     32.3
		AscendingNode   28
		ArgOfPericenter 237.3
		Epoch           2387700.197051
		MeanAnomaly     0
	}
}

//TCrB;WD STAR PRESENT

//TAU CrB;spanish wiki
//astrometric binary


Star "TAU CrB A/HIP 79119/HD 145328"
{
	ParentBody "TAU CrB"
	Class      "K1 III"
	Radius     4176000
	AppMagn    4.76
	MassSol    6
	Orbit
	{
		Period          261.241039
		SemiMajorAxis   5.865975
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "TAU CrB B"
{
	ParentBody "TAU CrB"
	Class      "M V" //unknown, related with     AppMagn
	AppMagn    13.2
	MassSol    0.5
	Orbit
	{
		Period          261.241039
		SemiMajorAxis   70.391694
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//TET CrB;english and spanish wiki


Star "TET CrB A/HIP 76127/HD 138749"
{
	ParentBody "TET CrB"
	Class      "B6 V"
	Radius     2296800
	AppMagn    4.14
	Orbit
	{
		Period          322.965069
		SemiMajorAxis   29.606557
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "TET CrB B"
{
	ParentBody "TET CrB"
	Class      "A2 V"
	AppMagn    6.6
	Orbit
	{
		Period          322.965069
		SemiMajorAxis   56.393443
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


//U CrB;english and spanish wiki
//semi detached close eclipsing binary


Star "U CrB A/HIP 74881/HD 136175"
{
	ParentBody "U CrB"
	Class      "B6 V"
	Radius     1809600
	AppMagn    7.82
	MassSol    4.7
	Orbit
	{
		Period          0.009452
		SemiMajorAxis   0.018857
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "U CrB B"
{
	ParentBody "U CrB"
	Class      "G0 III"
	Radius     3417360
	MassSol    1.41
	Orbit
	{
		Period          0.009452
		SemiMajorAxis   0.062858
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


//YY CrB;spanish wiki
//contact eclipsing binary


Star "YY CrB A/HIP 77958/HD 141990"
{
	ParentBody "YY CrB"
	Class      "F8 V"
	Radius     925680
	AppMagn    8.64
	MassSol    1.39
	Orbit
	{
		Period          0.001031
		SemiMajorAxis   0.002408
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "YY CrB B"
{
	ParentBody "YY CrB"
	Class      "F8 V"
	Radius     487200 
	MassSol    0.34
	Orbit
	{
		Period          0.001031
		SemiMajorAxis   0.009843
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


////////////////////////COMA BERENICES///////////////////////////////

//Diadem;6thCVB,english and spanish wiki
//very good system


Star "Diadem A/HIP 64241/HD 114378"
{
	ParentBody "Diadem"
	Class      "F5 V"
 
	AppMagn    4.85
	MassSol    1.25
	Orbit
	{
		Period          25.9704
		SemiMajorAxis   6.3901
		Eccentricity    0.4957
		Inclination     90.054
		AscendingNode   12.221
		ArgOfPericenter 101.689
		Epoch           2447651.8
		MeanAnomaly     0
	}
}

Star "Diadem B"
{
	ParentBody "Diadem"
	Class      "F5 V"
	AppMagn    5.53
	MassSol    1.25
	Orbit
	{
		Period          25.9704
		SemiMajorAxis   6.3901
		Eccentricity    0.4957
		Inclination     90.054
		AscendingNode   12.221
		ArgOfPericenter 281.689
		Epoch           2447651.8
		MeanAnomaly     0
	}
}


//CC Com;spanish wiki
//contact binary

Star "CC Com A"
{
	ParentBody "CC Com"
	Class      "K5 V"
	Radius     473280
	AppMagn    11.3
	MassSol    0.69
	Orbit
	{
		Period          0.000604
		SemiMajorAxis   0.0024
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "CC Com B"
{
	ParentBody "CC Com"
	Class      "K5 V"
	Radius     354960
	MassSol    0.36
	Orbit
	{
		Period          0.000604
		SemiMajorAxis   0.0046
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//EK Com;spanish wiki
//contact binary

Star "EK Com A"
{
	ParentBody "EK Com"
	Class      "K0 V"
	Radius     647280
	AppMagn    12.02
	MassSol    0.967
	Orbit
	{
		Period          0.00073
		SemiMajorAxis   0.002297
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "EK Com B"
{
	ParentBody "EK Com"
	Class      "K0 V"
	Radius     410640
	MassSol    0.338
	Orbit
	{
		Period          0.00073
		SemiMajorAxis   0.006571
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


//KR Com;6thCVB,spanish wiki
//AB contact binary


Barycenter "KR Com (AB)"
{
	ParentBody "KR Com"
	Orbit
	{
		Period          10.98
		SemiMajorAxis   2.8446
		Eccentricity    0.934
		Inclination     67.68
		AscendingNode   210.03
		ArgOfPericenter 301.8
		Epoch           2442055.1
		MeanAnomaly     0
	}
}

Star "KR Com A/HIP 65069/HD 115955"
{
	ParentBody "KR Com (AB)"
	Class      "F8 V"
	Radius     925680
	AppMagn    7.15
	MassSol    1.42
	Orbit
	{
		Period          0.00111704
		SemiMajorAxis   0.001
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "KR Com B"
{
	ParentBody "KR Com (AB)"
	Class      "F8 V"
	Radius     341040
	MassSol    0.13
	Orbit
	{
		Period          0.00111704
		SemiMajorAxis   0.0114
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "KR Com C"
{
	ParentBody "KR Com"
	Class      "G8 V"
	Radius     661200
	AppMagn    8.38
	MassSol    1
	Orbit
	{
		Period          10.98
		SemiMajorAxis   4.4063
		Eccentricity    0.934
		Inclination     67.68
		AscendingNode   210.03
		ArgOfPericenter 121.8
		Epoch           2442055.1
		MeanAnomaly     0
	}
}


///////////////////////////////////////VULPECULA///////////////////////////////////////////

//1 vul;spanish wiki

Barycenter "1 Vul (AB)"
{
	ParentBody "1 Vul"
	Orbit
	{
		Period          400000
		SemiMajorAxis   1049.2365
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "1 Vul A/HIP 94703/HD 180554"
{
	ParentBody "1 Vul (AB)"
	Class      "B4 IV"
	Radius     3271200
	AppMagn    4.76
	MassSol    6.5
	Orbit
	{
		Period          0.70910335
		SemiMajorAxis   0.23
		Eccentricity    0.63
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "1 Vul B"
{
	ParentBody "1 Vul (AB)"
	Class      "G V" //unknown related with mass
	MassSol    1.12
	Orbit
	{
		Period          0.70910335
		SemiMajorAxis   1.3347
		Eccentricity    0.63
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "1 Vul C"
{
	ParentBody "1 Vul"
	Class      "K V" //unknown, related with mass
	MassSol    0.8
	Orbit
	{
		Period          400000
		SemiMajorAxis   9993.9777
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//3 Vul;spanish wiki
//spect. binary

Star "3 Vul A/HIP 95260/HD 182255"
{
	ParentBody "3 Vul"
	Class      "B6 III"
	Radius     2018400
	AppMagn    5.22
	MassSol    4.2
	Orbit
	{
		Period          1.005503
		SemiMajorAxis   0.274568
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "3 Vul B"
{
	ParentBody "3 Vul"
	Class      "K V" //unknown,it could be also a WD
	MassSol    0.8
	Orbit
	{
		Period          1.005503
		SemiMajorAxis   1.441484
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//23 Vul;6thCVB, spanish wiki

Star "23 Vul A/HIP 99874/HD 192806"
{
	ParentBody "23 Vul"
	Class      "K3 III"
	Radius     19488000
	AppMagn    4.52
	Orbit
	{
		Period          25.33
		SemiMajorAxis   5.6011
		Eccentricity    0.4
		Inclination     71.5
		AscendingNode   97.5
		ArgOfPericenter 293.8
		Epoch           2455036.248818
		MeanAnomaly     0
	}
}

Star "23 Vul B"
{
	ParentBody "23 Vul"
	AppMagn    9 //unknown, SP companion
	Orbit
	{
		Period          25.33
		SemiMajorAxis   5.6011
		Eccentricity    0.4
		Inclination     71.5
		AscendingNode   97.5
		ArgOfPericenter 113.8
		Epoch           2455036.248818
		MeanAnomaly     0
	}
}


//30 Vul;Spanish wiki


Star "30 Vul A/HIP 102388/HD 197752"
{
	ParentBody "30 Vul"
	Class      "K2 III"
	Radius     11136000
	AppMagn    4.93
	Orbit
	{
		Period          7.134839
		SemiMajorAxis   1.005891 //unknown
		Eccentricity    0.38
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "30 Vul B"
{
	ParentBody "30 Vul"
	AppMagn    10 //unknown, SP companion
	Orbit
	{
		Period          7.134839
		SemiMajorAxis   4.023564 //unknown
		Eccentricity    0.38
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//ER Vul;Spanish wiki
//close binary

Star "ER Vul A"
{
	ParentBody "ER Vul"
	Class      "G0 V"
	Radius     1113600
	AppMagn    7.36
	MassSol    1.02
	Orbit
	{
		Period          0.001911
		SemiMajorAxis   0.009441
		Eccentricity    0.02
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "ER Vul B"
{
	ParentBody "ER Vul"
	Class      "G5 V"
	Radius     1085760
	MassSol    0.97
	Orbit
	{
		Period          0.001911
		SemiMajorAxis   0.009928
		Eccentricity    0.02
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//RR Vul;spanish wiki

Star "RR Vul A/HD 335394"
{
	ParentBody "RR Vul"
	Class      "A2 V"
	Radius     1461600
	AppMagn    10
	MassSol    3.15
	Orbit
	{
		Period          0.013828
		SemiMajorAxis   0.039344
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "RR Vul B"
{
	ParentBody "RR Vul"
	Class      "G3 III"
	Radius     3062400
	MassSol    2.05
	Orbit
	{
		Period          0.013828
		SemiMajorAxis   0.060455
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//Z Vul;spanish wiki
//Semi-detached close binary

Star "Z Vul A/HIP 95163/HD 181987"
{
	ParentBody "Z Vul"
	Class        "B4 V"
	Radius       3132000
	AppMagn      7.33
	MassSol      5.4
	Orbit
	{
		Period            0.006721
		SemiMajorAxis     0.021005
		ArgOfPericenter   0
		MeanAnomaly       0
	}
}

Star "Z Vul B"
{
	ParentBody "Z Vul"
	Class      "A5 III"
	Radius     3201600
	MassSol    2.3
	Orbit
	{
		Period            0.006721
		SemiMajorAxis     0.049316
		ArgOfPericenter   180
		MeanAnomaly       0
	}
}

//T Vul;spanish and english wiki


//Article
//Title:   Binary Cepheids: Separations and Mass Ratios in 5 Mass Sol Binaries
//Authors: Nancy Remage Evan, Howard E. Bond, Gail H. Schaefer, Brian D. Mason,
//Margarita Karovska, and Evan Tingle

Star "T Vul A/HD 198726"
{
	ParentBody "T Vul"
	Class      "F5 Ib"
	Radius     19488000
	AppMagn    5.61
	MassSol    5.25				//q = 0.4
	Orbit
	{
		Period          217.475218
		SemiMajorAxis   178.271429
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "T Vul B"
{
	ParentBody "T Vul"
	Class      "A0.8 V"
	MassSol    2.1
	Orbit
	{
		Period          217.475218
		SemiMajorAxis   445.678571
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}




///////////////////////////LACERTA//////////////////////////////////

//G 216-7;with brown dwarf

//2 Lac;english and spanish wiki
//close binary 

Star "2 Lac A/HD 212120 A"
{
	ParentBody "2 Lac"
	Class      "B6 V"
	Radius     2992800
	AppMagn    4.55
	MassSol    5
	Orbit
	{
		Period          0.007163
		SemiMajorAxis   0.031294
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "2 Lac B"
{
	ParentBody "2 Lac"
	Class      "B8 V"
	Radius     2296800
	MassSol    3.5
	Orbit
	{
		Period          0.007163
		SemiMajorAxis   0.044706
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//5 Lac;english and spanish wiki

Star "5 Lac A/HIP 111022 A/HD 213311 A"
{
	ParentBody "5 Lac"
	Class      "M0 II"
	Radius     190008000
	AppMagn    4.36
	MassSol    8.5
	Orbit
	{
		Period          56.827944
		SemiMajorAxis   9.640666
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "5 Lac B"
{
	ParentBody "5 Lac"
	Class      "B8 V"
	MassSol    3.4
	Orbit
	{
		Period          56.827944
		SemiMajorAxis   24.101665
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//ADS 16402;It has an exoplanet HAT-P-1 b

//AR Lac;spanish wiki
//eclipsing binary

Star "AR Lac A/HD 210337 A"
{
	ParentBody "AR Lac"
	Class      "G2 IV"
	Radius     1057920
	AppMagn    6.09
	MassSol    1.23
	Orbit
	{
		Period          0.005429
		SemiMajorAxis   0.021295
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "AR Lac B"
{
	ParentBody "AR Lac"
	Class      "K0 III"
	Radius     1893120
	MassSol    1.27
	Orbit
	{
		Period          0.005429
		SemiMajorAxis   0.020625
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//CM Lac;spanish wiki


Star "CM Lac A/HIP 108606 A/HD 209147 A"
{
	ParentBody "CM Lac"
	Class      "A2 V"
	Radius     1106640
	AppMagn    8.18
	MassSol    1.88
	Orbit
	{
		Period          0.004393
		SemiMajorAxis   0.019307
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "CM Lac B"
{
	ParentBody "CM Lac"
	Class      "A8 V"
	Radius     988320
	MassSol    1.47
	Orbit
	{
		Period          0.004393
		SemiMajorAxis   0.024693
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//RW Lac;spanish wiki

Star "RW Lac A"
{
	ParentBody "RW Lac"
	Class      "G5 V"
	Radius     828240
	AbsMagn    4.46
	MassSol    0.93
	Orbit
	{
		Period          0.028392
		SemiMajorAxis   0.054712
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "RW Lac B"
{
	ParentBody "RW Lac"
	Class      "G7 V"
	Radius     668160
	AbsMagn    5.1
	MassSol    0.87
	Orbit
	{
		Period          0.028392
		SemiMajorAxis   0.058485
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//V364 Lac;spanish wiki

Star "V364 Lac A/HIP 112928 A/HD 216429 A"
{
	ParentBody "V364 Lac"
	Class      "A4 V"
	Radius     2296800
	AbsMagn    0.57
	MassSol    2.33
	Orbit
	{
		Period          0.020129
		SemiMajorAxis   0.061259
		Eccentricity    0.29
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "V364 Lac B"
{
	ParentBody "V364 Lac"
	Class      "A3 V"
	Radius     2088000
	AbsMagn    0.68
	MassSol    2.3
	Orbit
	{
		Period          0.020129
		SemiMajorAxis   0.062058
		Eccentricity    0.29
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//Z Lac;spanish wiki
//spectroscopic binary, only known period

Star "Z Lac A/HIP 111972 A/HD 214975 A"
{
	ParentBody "Z Lac"
	Class      "F6 Ib"
	Radius     47328000
	AppMagn    8.57
	MassSol    4
	Orbit
	{
		Period          1.047502
		SemiMajorAxis   0.189183
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Z Lac B"
{
	ParentBody "Z Lac"
	Class	   "M V" //unknown
	Orbit
	{
		Period          1.047502
		SemiMajorAxis   1.513467
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

////////////////////////LEO MINOR//////////////////////////////////////

//11 LMi;6thCVB,english,spanish wiki

Star "11 LMi A/HIP 47080 A/HD 82885 A"
{
	ParentBody "11 LMi"
	Class      "G8 V"
	Radius     698018.4
	AppMagn    4.8
	MassSol    0.964
	Orbit
	{
		Period          201
		SemiMajorAxis   8.3954
		Eccentricity    0.88
		Inclination     117
		AscendingNode   41.3
		ArgOfPericenter 170
		Epoch           2432551.939061
		MeanAnomaly     0
	}
}

Star "11 LMi B"
{
	ParentBody "11 LMi"
	Class      "M5 V"
	AppMagn    12.5
	MassSol    0.23
	Orbit
	{
		Period          201
		SemiMajorAxis   35.1875
		Eccentricity    0.88
		Inclination     117
		AscendingNode   41.3
		ArgOfPericenter 350
		Epoch           2432551.939061
		MeanAnomaly     0
	}
}

//20 LMi;english and spanish wiki

Star "20 LMi A/HIP 49081 A/HD 86728 A"
{
	ParentBody "20 LMi"
	Class      "G3 V"
	Radius     835200
	AppMagn    5.4
	MassSol    1.02
	Orbit
	{
		Period          3003.611715
		SemiMajorAxis   19.300504
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "20 LMi B"
{
	ParentBody "20 LMi"
	Class      "M6.5V" 			//it could be also binary itself
	MassSol    0.1
	Orbit
	{
		Period          3003.611715
		SemiMajorAxis   196.86514
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//BET LMi;6thCVB,english and spanish wiki

Star "BET LMi A/HIP 51233 A/HD 90537 A"
{
	ParentBody "BET LMi"
	Class      "G9 III"
	Radius     5428800
	AppMagn    4.62
	MassSol    2
	Orbit
	{
		Period          38.62
		SemiMajorAxis   6.5514
		Eccentricity    0.668
		Inclination     79.1
		AscendingNode   41.5
		ArgOfPericenter 29.8
		Epoch           2451234.077529
		MeanAnomaly     0
	}
}

Star "BET LMi B"
{
	ParentBody "BET LMi"
	Class      "F8 V"
	Radius     1392000
	AppMagn    6.04
	MassSol    1.35
	Orbit
	{
		Period          38.62
		SemiMajorAxis   9.7057
		Eccentricity    0.668
		Inclination     79.1
		AscendingNode   41.5
		ArgOfPericenter 209.8
		Epoch           2451234.077529
		MeanAnomaly     0
	}
}

//HD 87822;6thCVB,english wiki

Star "HD 87822 A/HIP 49658 A"
{
	ParentBody "HD 87822"
	Class      "F4 V"
	AppMagn    6.9
	MassSol    1.37
	Orbit
	{
		Period          17.765
		SemiMajorAxis   4.7883
		Eccentricity    0.396
		Inclination     84.59
		AscendingNode   349.57
		ArgOfPericenter 7.73
		Epoch           2447575.446424
		MeanAnomaly     0
	}
}

Star "HD 87822 B"
{
	ParentBody "HD 87822"
	Class      "F V"	//unknown related with appmagn
	AppMagn    7.2
	Orbit
	{
		Period          17.765
		SemiMajorAxis   4.7883
		Eccentricity    0.396
		Inclination     84.59
		AscendingNode   349.57
		ArgOfPericenter 187.73
		Epoch           2447575.446424
		MeanAnomaly     0
	}
}

//SX LMi;WD present

//////////////////////////////////CANES VENACITI///////////////////////////////////

//25 CVn;6thCVB,english wiki

Star "25 CVn A/HIP 66458 A/HD 118623 A"
{
	ParentBody "25 CVn"
	Class      "A7 III"
	AppMagn    4.98
	MassSol    2		//unknown, guess, more evolved
	Orbit
	{
		Period          228
		SemiMajorAxis   26.5129
		Eccentricity    0.8
		Inclination     147
		AscendingNode   87
		ArgOfPericenter 159
		Epoch           2401871.594364
		MeanAnomaly     0
	}
}

Star "25 CVn B"
{
	ParentBody "25 CVn"
	Class      "F0 V"
	AppMagn    6.95
	MassSol    1.58
	Orbit
	{
		Period          228
		SemiMajorAxis   33.5607
		Eccentricity    0.8
		Inclination     147
		AscendingNode   87
		ArgOfPericenter 339
		Epoch           2401871.594364
		MeanAnomaly     0
	}
}

//Cor Caroli;english and spanish wiki

Star "Cor Caroli A/ALF2 CVn/HIP 63121 A/HD 112412 A"
{
	ParentBody "Cor Caroli"
	Class      "F0 V"
	Radius     2853600
	AppMagn    2.84
	MassSol    2.8
	Orbit
	{
		Period          7901.626375
		SemiMajorAxis   236.363636
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Cor Caroli B/ALF1 CVn"
{
	ParentBody "Cor Caroli"
	Class      "A0 V"
	Radius     897840
	AppMagn    5.6
	MassSol    1.6
	Orbit
	{
		Period          7901.626375
		SemiMajorAxis   413.636364
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//GH CVn;spanish wiki

Star "GH CVn A/HIP 66257 A/HD 118216 A"
{
	ParentBody "GH CVn"
	Class      "A6 V"
	Radius     2088000
	AppMagn    4.93
	MassSol    1.5
	Orbit
	{
		Period          0.007155
		SemiMajorAxis   0.017046
		Eccentricity    0.04
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "GH CVn B"
{
	ParentBody "GH CVn"
	Class      "K2 IV"
	Radius     2436000
	MassSol    0.8
	Orbit
	{
		Period          0.007155
		SemiMajorAxis   0.03196
		Eccentricity    0.04
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//BI CVn;spanish wiki
//contact binary
//probably triple;third compact low mass companion

Star "BI CVn A/HIP 63701 A"
{
	ParentBody "BI CVn"
	Class      "F8 V"
	Radius     953520
	AppMagn    10.36
	MassSol    1.59
	Orbit
	{
		Period          0.00106
		SemiMajorAxis   0.003947
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "BI CVn B"
{
	ParentBody "BI CVn"
	Class      "F8 V"
	Radius     640320
	MassSol    0.65
	Orbit
	{
		Period          0.00106
		SemiMajorAxis   0.009655
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//DG CVn;english wiki

Star "DG CVn A"
{
	ParentBody "DG CVn"
	Class      "M4 V"
	AppMagn    12.02
	Orbit
	{
		Period          9.860659
		SemiMajorAxis   1.8
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "DG CVn B"
{
	ParentBody "DG CVn"
	Class      "M4 V"	//unknown but probably another red dwarf
	Orbit
	{
		Period          9.860659
		SemiMajorAxis   1.8
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//RS CVn;spanish wiki

Star "RS CVn A/HIP 64293 A/HD 114519 A"
{
	ParentBody "RS CVn"
	Class      "G8 IV"
	Radius     2784000
	AppMagn    7.93
	MassSol    1.44
	Orbit
	{
		Period          0.013136
		SemiMajorAxis   0.039046
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "RS CVn B"
{
	ParentBody "RS CVn"
	Class      "F6 IV"
	Radius     1308480
	MassSol    1.41
	Orbit
	{
		Period          0.013136
		SemiMajorAxis   0.039877
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

/////////////////////////////////////LYNX////////////////////////////////



//2 Lyn;6thCVB,spanish wiki

Star "2 Lyn A/HIP 30060 A/HD 43378 A"
{
	ParentBody "2 Lyn"
	Class      "A2 V"
	Radius     1531200
	AppMagn    4.45
	MassSol    2.3
	Orbit
	{
		Period          2.2448
		SemiMajorAxis   0.041
		Eccentricity    0.2984
		Inclination     59.8
		AscendingNode   52.52
		ArgOfPericenter 298.31
		Epoch           2448160.7305
		MeanAnomaly     0
	}
}

Star "2 Lyn B"
{
	ParentBody "2 Lyn"
	Class      "M4 V"   //unknown
	MassSol    0.27     //minimum mass
	Orbit
	{
		Period          2.2448
		SemiMajorAxis   0.3489
		Eccentricity    0.2984
		Inclination     59.8
		AscendingNode   52.52
		ArgOfPericenter 118.31
		Epoch           2448160.7305
		MeanAnomaly     0
	}
}

//10 UMa;6thCVB,english and spanish wiki
//Previously inside Ursae Majoris, now in Lynx

Star "10 UMa A/HIP 44248 A/HD 76943 A"
{
	ParentBody "10 UMa"
	Class      "F4 V"
	Radius     1002240
	AppMagn    4.18
	MassSol    2.54
	Orbit
	{
		Period          21.0568
		SemiMajorAxis   3.1123
		Eccentricity    0.15075
		Inclination     131.366
		AscendingNode   203.74
		ArgOfPericenter 32.3
		Epoch           2449262.6
		MeanAnomaly     0
	}
}

Star "10 UMa B"
{
	ParentBody "10 UMa"
	Class      "G5 V"
	Radius     696000
	AppMagn    6.48
	MassSol    1.1
	Orbit
	{
		Period          21.0568
		SemiMajorAxis   7.1866
		Eccentricity    0.15075
		Inclination     131.366
		AscendingNode   203.74
		ArgOfPericenter 212.3
		Epoch           2449262.6
		MeanAnomaly     0
	}
}

//12 Lyn;6tCVB,spanish wiki

Barycenter "12 Lyn (AB)"
{
	ParentBody "12 Lyn"
	Orbit
	{
		Period          5640
		SemiMajorAxis   138.4969
		Inclination     134.7
		AscendingNode   166.5
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "12 Lyn A/HIP 32438 A/HD 48250 A"
{
	ParentBody "12 Lyn (AB)"
	Class      "A1 V"
	AppMagn    5.52
	MassSol    2.5
	Orbit
	{
		Period          907.6
		SemiMajorAxis   65.5013
		Eccentricity    0.37
		Inclination     134.7
		AscendingNode   166.5
		ArgOfPericenter 322.6
		Epoch           2698959.598852
		MeanAnomaly     0
	}
}

Star "12 Lyn B"
{
	ParentBody "12 Lyn (AB)"
	Class      "A2 V"
	AppMagn    6.07
	MassSol    1.9
	Orbit
	{
		Period          907.6
		SemiMajorAxis   86.1859
		Eccentricity    0.37
		Inclination     134.7
		AscendingNode   166.5
		ArgOfPericenter 142.6
		Epoch           2698959.598852
		MeanAnomaly     0
	}
}

Star "12 Lyn C"
{
	ParentBody "12 Lyn"
	Class      "F V"	//unknown
	AppMagn    7.34
	MassSol    1.4
	Orbit
	{
		Period          5640
		SemiMajorAxis   435.2761
		Inclination     134.7		//just aligned
		AscendingNode   166.5		//just aligned
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//15 Lyn;6thCVB,spanish wiki

Star "15 Lyn A/HIP 33449 A/HD 50522 A"
{
	ParentBody "15 Lyn"
	Class      "G5 III"
	Radius     5568000
	AppMagn    4.7
	MassSol    2.5
	Orbit
	{
		Period          262
		SemiMajorAxis   25.6378
		Eccentricity    0.74
		Inclination     78
		AscendingNode   43.4
		ArgOfPericenter 98
		Epoch           2448870.960503
		MeanAnomaly     0
	}
}

Star "15 Lyn B"
{
	ParentBody "15 Lyn"
	Class      "A V"	//unknown, related with appmagn
	AppMagn    5.7
	MassSol    1.76
	Orbit
	{
		Period          262
		SemiMajorAxis   36.4174
		Eccentricity    0.74
		Inclination     78
		AscendingNode   43.4
		ArgOfPericenter 278
		Epoch           2448870.960503
		MeanAnomaly     0
	}
}

//38 Lyn;spanish wiki

Star "38 Lyn A/HIP 45688 A/HD 80081 A"
{
	ParentBody "38 Lyn"
	Class      "A3 V"
	Radius     1670400
	AppMagn    3.92
	MassSol    2.2
	Orbit
	{
		Period          505.935218
		SemiMajorAxis   37.839127
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "38 Lyn B"			//probably also double
{
	ParentBody "38 Lyn"
	Class      "F4 V"
	AppMagn    6.09
	MassSol    1.4
	Orbit
	{
		Period          505.935218
		SemiMajorAxis   59.461486
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//AE Lyn;spanish wiki

Star "AE Lyn A/HIP 39348 A/HD 65626 A"
{
	ParentBody "AE Lyn"
	Class      "F8 V"
	Radius     2227200
	AppMagn    6.49
	MassSol    1.59
	Orbit
	{
		Period          0.027707
		SemiMajorAxis   0.067599
		Eccentricity    0.11
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "AE Lyn B"
{
	ParentBody "AE Lyn"
	Class      "F8 V"
	Radius     2575200
	MassSol    1.6
	Orbit
	{
		Period          0.027707
		SemiMajorAxis   0.067176
		Eccentricity    0.11
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//CN Lyn;spanish wiki
//triple but unknown data of the third component

Star "CN Lyn A/HIP 39250 A"
{
	ParentBody "CN Lyn"
	Class      "F3 V"
	Radius     1252800
	AppMagn    9.1
	MassSol    1.04
	Orbit
	{
		Period          0.005354
		SemiMajorAxis   0.01953
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "CN Lyn B"
{
	ParentBody "CN Lyn"
	Class      "F3 V"
	Radius     1280640
	MassSol    1.04
	Orbit
	{
		Period          0.005354
		SemiMajorAxis   0.01953
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//RR Lyn;spanish wiki
//proposed 3rd component

Star "RR Lyn A/HD 44691 A"
{
	ParentBody "RR Lyn"
	Class      "A6 IV"
	Radius     1788720
	AppMagn    5.5
	MassSol    1.93
	Orbit
	{
		Period          0.027228
		SemiMajorAxis   0.059965
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "RR Lyn B"
{
	ParentBody "RR Lyn"
	Class      "F0 V"
	Radius     1106640
	MassSol    1.51
	Orbit
	{
		Period          0.027228
		SemiMajorAxis   0.076644
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//XO-2;already in SE 0.974

/////////////////////////////////LYRA////////////////////////////////////

//17 Lyr;english wiki, 9thCSBO
//taking separation of 3.7" of the WDS catalog 

Star "17 Lyr A/HIP 93917 A/HD 178449 A"
{
	ParentBody "17 Lyr"
	Class      "F0 V"
	AppMagn    5.229
	MassSol    1.58
	Orbit
	{
		Period          1174.126698
		SemiMajorAxis   52.803983
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "17 Lyr B"
{
	ParentBody "17 Lyr"
	Class      "K V"
	AppMagn    9.1
	MassSol    0.86
	Orbit
	{
		Period          1174.126698
		SemiMajorAxis   97.011968
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//BD+36 3317;spanish wiki
//detached close binary

Star "BD+36 3317 A"
{
	ParentBody "BD+36 3317"
	Class      "A0 V"
	AppMagn    8.77
	MassSol    2.1
	Orbit
	{
		Period          0.011779
		SemiMajorAxis   0.03903
		Inclination     90
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "BD+36 3317 B"
{
	ParentBody "BD+36 3317"
	Class      "A5 V"
	Orbit
	{
		Period          0.011779
		SemiMajorAxis   0.043138	//for a typical mass of a A5V and period 
		Inclination     90			//near 90є
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//Sheliak;6thCVB,english wiki
//contact binary, confirmed accretion disk around B

Star "Sheliak A/HIP 92420 A/HD 174638 A"
{
	ParentBody "Sheliak"
	Class      "B V"
	Radius     4176000
	AppMagn    3.5
	MassSol    13.16
	Orbit
	{
		Period          0.0354
		SemiMajorAxis   0.0529
		Eccentricity    0
		Inclination     92.1
		AscendingNode   253.22
		ArgOfPericenter 0
		Epoch           2454283.043
		MeanAnomaly     0
	}
}

Star "Sheliak B"
{
	ParentBody "Sheliak"
	Class      "B7 II"
	Radius     10509600
	AppMagn    4
	MassSol    2.97
	Orbit
	{
		Period          0.0354
		SemiMajorAxis   0.2345
		Eccentricity    0
		Inclination     92.1
		AscendingNode   253.22
		ArgOfPericenter 180
		Epoch           2454283.043
		MeanAnomaly     0
	}
}

//DEL1 Lyr;english wiki

Star "DEL1 Lyr A/HIP 92728 A/IDS 18402+3650 A"
{
	ParentBody "DEL1 Lyr"
	Class      "B2.5 V"
	AppMagn    5.569
	Orbit
	{
		Period          0.241895	
		SemiMajorAxis   0.406694 //unknown
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "DEL1 Lyr B/IDS 18402+3650 B"
{
	ParentBody "DEL1 Lyr"
	Class      "K2 III"
	AppMagn    9.8
	Orbit
	{
		Period          0.241895
		SemiMajorAxis   0.406694
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//EPS Lyr;6thCVB, english wiki
//5th component discovered by interferometry (Ca)

Barycenter "EPS1 Lyr"
{
	ParentBody "EPS Lyr"
	Orbit
	{
		Period          382882.803
		SemiMajorAxis   5176.9912
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Barycenter "EPS2 Lyr"
{
	ParentBody "EPS Lyr"
	Orbit
	{
		Period          382882.803
		SemiMajorAxis   5323.0088
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "EPS Lyr A/HIP 91919 A/HD 173482 A"
{
	ParentBody "EPS1 Lyr"
	Class      "A2 V"
	AppMagn    5.02
	Orbit
	{
		Period          0.0354
		SemiMajorAxis   0.0231
		Inclination     92.1
		AscendingNode   253.22
		ArgOfPericenter 0
		Epoch           2454283.043
		MeanAnomaly     0
	}
}

Star "EPS Lyr B"
{
	ParentBody "EPS1 Lyr"
	Class      "A4 V"
	AppMagn    6.02
	Orbit
	{
		Period          0.0354
		SemiMajorAxis   0.0254
		Inclination     92.1
		AscendingNode   253.22
		ArgOfPericenter 180
		Epoch           2454283.043
		MeanAnomaly     0
	}
}

Star "EPS Lyr C"
{
	ParentBody "EPS2 Lyr"
	Class      "A3 V"
	AppMagn    5.14
	Orbit
	{
		Period          724.307
		SemiMajorAxis   70.6918
		Eccentricity    0.353
		Inclination     126.14
		AscendingNode   26.23
		ArgOfPericenter 73.78
		Epoch           2533334.67994
		MeanAnomaly     0
	}
}

Star "EPS Lyr D"
{
	ParentBody "EPS2 Lyr"
	Class      "A5 V"
	AppMagn    5.37
	Orbit
	{
		Period          724.307
		SemiMajorAxis   74.4125
		Eccentricity    0.353
		Inclination     126.14
		AscendingNode   26.23
		ArgOfPericenter 253.78
		Epoch           2533334.67994
		MeanAnomaly     0
	}
}

//HD 171301;english wiki

Star "HD 171301 A/HIP 90923 A/BD+30 3223 A"
{
	ParentBody "HD 171301"
	Class      "B8 IV"
	AppMagn    5.465
	Orbit
	{
		Period          11230.845542
		SemiMajorAxis   145.631606
		Inclination     157
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "BD+30 3223 B/HD 171301 B"
{
	ParentBody "HD 171301"
	Class      "K V"					//unknown, related with appmagn
	AppMagn    12.7			
	Orbit
	{
		Period          11230.845542
		SemiMajorAxis   660.196615
		Inclination     157
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//HD 172044;english wiki

Star "HD 172044 A/HIP 91235 A/BD+33 3154 A"
{
	ParentBody "HD 172044"
	Class      "B8 III"
	AppMagn    5.41
	Orbit
	{
		Period          19870.358539	//unknown due lack of data about the giant
		SemiMajorAxis   291.104294
		Inclination     205
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "BD+33 3154 B/HD 172044 B"
{
	ParentBody "HD 172044"				
	Class      "G V"				  //unknown,related with appmagn
	AppMagn    10.7
	Orbit
	{
		Period          19870.358539	//unknown due lack of data about the giant
		SemiMajorAxis   873.312883
		Inclination     205
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//HD 176051;already in SE 0.974
//HD 178911;already in SE 0.974
//Kepler-14 in exoplanetssuns-bin

//ZET Lyr;english wiki, professor Jim Kaler stars website

Barycenter "ZET1 Lyr/ZET Lyr A/6 Lyr/HD 173648/HIP 91971/HR 7056"
{
	ParentBody "ZET Lyr"
	Orbit
	{
		Period          41292.02
		SemiMajorAxis   723.4043
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "ZET Lyr Aa"
{
	ParentBody "ZET1 Lyr"
	Class      "A5 V"
	Radius     1948800
	AppMagn    4.36
	MassSol    2.2
	Orbit
	{
		Period          0.01177276
		SemiMajorAxis   0.0187
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "ZET Lyr Ab"
{
	ParentBody "ZET1 Lyr"
	Class      "M V"		//unknown SP binary, it could be also a WD
	MassSol    0.8
	Orbit
	{
		Period          0.01177276
		SemiMajorAxis   0.0513
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "ZET2 Lyr/ZET Lyr B/7 Lyr/HD 173649/HIP 91973/HR 7057"
{
	ParentBody "ZET Lyr"
	Class      "F0 IV"
	Radius     1183200
	AppMagn    5.23
	MassSol    1.7
	Orbit
	{
		Period          41292.02
		SemiMajorAxis   1276.5957
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//HD 181068
//Paper: 
//HD 181068: A Red Giant in a Triply Eclipsing Compact Hierarchical Triple System
//8 APRIL 2011 VOL 332 SCIENCE www.sciencemag.org

Barycenter "HD 181068 (BC)"
{
	ParentBody "HD 181068"
	Orbit
	{
	   Period          0.12462094
		SemiMajorAxis   0.2787
		ArgOfPericenter 0
		Epoch           2455454.573
		MeanAnomaly     0
	}
}

Star "HD 181068 A/HIP 94780 A"
{
	ParentBody "HD 181068"
	Class      "G8 III"
	Radius     8630400
	AbsMagn    -0.3
	MassSol    3
	Orbit
	{
		Period          0.12462094
		SemiMajorAxis   0.1301
		ArgOfPericenter 180
		Epoch           2455454.573
		MeanAnomaly     0
	}
}

Star "HD 181068 B"
{
	ParentBody "HD 181068 (BC)"
	Class      "G8 V"
	AbsMagn    5.6
	MassSol    0.7
	Orbit
	{
		Period          0.00247959
		SemiMajorAxis   0.0109
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "HD 181068 C"
{
	ParentBody "HD 181068 (BC)"
	Class      "K1 V"
	AbsMagn    6.1
	MassSol    0.7
	Orbit
	{
		Period          0.00247959
		SemiMajorAxis   0.0109
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}