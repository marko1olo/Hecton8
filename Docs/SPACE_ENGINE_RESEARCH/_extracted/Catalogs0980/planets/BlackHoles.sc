// Star solver log level:
// 0 - do not log
// 1 - log errors and warnings only
// 2 - log everything
LogLevel    1

///////////////////////////////////////////////////////////
//               Stellar mass black holes                //
///////////////////////////////////////////////////////////

Star	"SS 433 A"
{
	ParentBody	"SS 433"
	Class		"A7 Ib"
	MassSol     17.7

	Orbit
	{
		Period			0.035817
		//SemiMajorAxis	0.00187 // mass ratio * 0.0072 AU
		Eccentricity	0
		Inclination		78.8
		AscendingNode	25	// random
		ArgOfPericen	0	// random
		MeanAnomaly     0	// random
	}
}

Star	"SS 433 B"
{
	ParentBody	"SS 433"
	Class		"BlackHole"
	MassSol     5.3

	AccretionDisk
	{
		Radius        6.66e-4	// AU
		//Radius        0.002838  // AU
		Temperature   50000
		AccretionRate 1e-7		// Msol/year
		Brightness    1.0
		Density       5000
		PrecessionPeriod 0.4449	// days
		NutationPeriod   0.0172	// days
	}

	Orbit
	{
		Period			0.035817
		//SemiMajorAxis	0.00534 // mass ratio * 0.0072 AU
		Eccentricity	0
		Inclination		78.8
		AscendingNode	25	// random
		ArgOfPericen	180	// random
		MeanAnomaly     0	// random
	}
}

Star "XTE J1859+226 A"
{
	ParentBody "XTE J1859+226"
	Class      "X"
	MassSol    9.8
	Orbit
	{
		Period 		    0.0010468037
		SemiMajorAxis   0.00309735
		ArgOfPericenter 0
		MeanAnomaly     0   
	}
	AccretionDisk { }
}

Star "V404 Vul"
{
	ParentBody "XTE J1859+226"
	Class      "G5 V"  					//unknown subclass
	Radius     738500
	MassSol	   1.5
	Orbit
	{
		Period          0.0010468037
		SemiMajorAxis   0.02023599
		ArgOfPericenter 180
		MeanAnomaly     0
	} 
}

Star "4U 1755-338 A"
{
	ParentBody "4U 1755-338"
	Class      "X"
	MassSol    3  						//not confirmed
	Orbit
	{
		Period          0.0005022831
		SemiMajorAxis   0.00046556
		ArgOfPericenter 0
		MeanAnomaly     0
	}
	AccretionDisk
	{
		Brightness 1.0
	}
}

Star "V4134 Sgr"
{
	ParentBody "4U 1755-338"
	Class      "M1 V"
	AbsMagn    5.31
	MassSol    0.42 
	Orbit
	{
		Period          0.0005022831
		SemiMajorAxis   0.00886778
		ArgOfPericenter 180
		MeanAnomaly     0
	} 
}

Star "3U 0042+32 A/2A 0042+323 A"
{
	ParentBody "3U 0042+32"
	Class      "X"
	Orbit
	{
		Period          0.0317351598
		SemiMajorAxis   0.00164609
		ArgOfPericenter 0
		MeanAnomaly     0
	}
	AccretionDisk
	{
		Brightness 1.0
	}
}

Star "3U 0042+32 B"
{
	ParentBody "3U 0042+32"
	Class      "G V"
	Orbit
	{
		Period          0.0317351598
		SemiMajorAxis   0.13168724
		ArgOfPericenter 180
		MeanAnomaly     0
	} 
}


Star "XTE J1118+480 A"
{
	ParentBody "XTE J1118+480"
	Class      "X"
	MassSol    6.5
	Orbit
	{
		Period          0.0004655594
		SemiMajorAxis   0.00050000
		ArgOfPericenter 0
		MeanAnomaly     0
	}
	AccretionDisk
	{
		Brightness 1.0
	}
}

Star "KV UMa"
{
	ParentBody "XTE J1118+480"
	Class      "K7V"
	Radius     490000
	MassSol    0.3
	Orbit
	{
		Period          0.0004655594
		SemiMajorAxis   0.01083333
		ArgOfPericenter 180
		MeanAnomaly     0
	} 
}

Star   "XTE J1650-500 A"
{
	ParentBody	"XTE J1650-500"
	Class		"X"
	MassSol     3.8
	Orbit
	{  
		Period          0.00087671
		ArgOfPericenter 0
		SemiMajorAxis   0.00711002
		MeanAnomaly     0

	}
	AccretionDisk
	{
		Brightness 1.0
	}
}

Star   "XTE J1650-500 B"
{
	ParentBody "XTE J1650-500"
	Class      "B V"
	MassSol    2.7
	Orbit
	{  
		Period          0.00087671
		ArgOfPericenter 180
		SemiMajorAxis   0.01000669
		MeanAnomaly     0
	}
}

Star   "M 33 X-7 A"
{
	ParentBody "M 33 X-7"
	Class	   "X"
	MassSol    15.7
	Orbit
	{  
		Period          0.00945205
		ArgOfPericenter 0
		SemiMajorAxis   0.16119046
		MeanAnomaly     0
	}
	AccretionDisk
	{
		Brightness 1.0
	}
}

Star   "M 33 X-7 B"
{
	ParentBody "M 33 X-7"
	Class      "WN"
	Radius     16100000	 			//generic radius for the biggest Wolf Rayet stars
	MassSol    70
	Orbit
	{  
		Period          0.00945205
		ArgOfPericenter 180
		SemiMajorAxis   0.03615272
		MeanAnomaly     0
	}
}

Star   "IGR J17091-3624 A"
{
	ParentBody "IGR J17091-3624"
	Class      "X"
	MassSol    5  					//between 3 and 10 Ms
	Orbit
	{  
		Period          0.00027397
		ArgOfPericenter 0
		SemiMajorAxis   0.00054888
		MeanAnomaly     0
	}
	AccretionDisk
	{
		Brightness 1.0
	}
}

Star   "IGR J17091-3624 B"
{
	ParentBody "IGR J17091-3624"
	Class      "M5 V" 				//unknown
	Orbit
	{  
		Period          0.00027397
		ArgOfPericenter 180
		SemiMajorAxis   0.00686104
		MeanAnomaly     0
	}
}

//sources//
//Mark J. Burke et al. 2012 Apj 749 112. Web article: iopscience.iop.org/0004-637X/749/2/112 //

Star   "CXOU J132527.6-430023 A"
{
	ParentBody "CXOU J132527.6-430023"
	Class      "X"
	MassSol    10
	Orbit
	{  
		Period          0.00027397 		//fictional
		ArgOfPericenter 0 				
		SemiMajorAxis   0.00035458 		//fictional
		MeanAnomaly     0
	}
	AccretionDisk
	{
		Brightness 1.0
	}
}

Star   "CXOU J132527.6-430023 B"
{
	ParentBody "CXOU J132527.6-430023"
	Class      "M5 V"	 				//suggested low mass B in Mark J. Burke papers
	Orbit
	{  
		Period          0.00027397 	 	//fictional
		ArgOfPericenter 180 		
		SemiMajorAxis   0.00886462 		//fictional
		MeanAnomaly     0
	}
}

Star "RX J0042.3+4115 A"
{
	ParentBody "RX J0042.3+4115"
	Class      "X"
	Orbit
	{
		SemiMajorAxis   0.01 			//generic
		ArgOfPericenter 0
		MeanAnomaly     0
	}
	AccretionDisk
	{
		Brightness 1.0
	}
}

Star "RX J0042.3+4115 B"
{
	ParentBody "RX J0042.3+4115"
	Class      "M5 V" 					//unknown
	Orbit
	{
		SemiMajorAxis   0.01 			//generic
		ArgOfPericenter 180
		MeanAnomaly     0
	} 
}

Star "GRO J0422+32 A/Nova Per 1992"
{
	ParentBody "GRO J0422+32"
	Class      "X"
	MassSol    4.3
	Orbit
	{
		Period          0.0005812557
		SemiMajorAxis   0.00113684
		ArgOfPericenter 0
		MeanAnomaly     0
	}
	AccretionDisk
	{
		Brightness 1.0
	}
}

Star "V418 Per"
{
	ParentBody "GRO J0422+32"
	Class      "M2 V"
	AbsMagn    10.50
	MassSol	   0.45
	Orbit
	{
		Period          0.0005812557
		SemiMajorAxis   0.01086316
		ArgOfPericenter 180
		MeanAnomaly     0
	} 
}


Star "LMC X-3 A"
{
	ParentBody "LMC X-3"
	Class      "X"
	MassSol    7.6
	Orbit
	{
		Period          0.0046706621
		SemiMajorAxis   0.02380165
		ArgOfPericenter 0
		MeanAnomaly     0
	}
	AccretionDisk
	{
		Brightness 1.0
	}
}

Star "LMC X-3 B"
{
	ParentBody "LMC X-3"
	Class      "B3 V"
	AbsMagn    -2.01 
	MassSol	   4.5
	Orbit
	{
		Period          0.0046706621
		SemiMajorAxis   0.04019835
		ArgOfPericenter 180
		MeanAnomaly     0
	} 
}

Star "LMC X-1 A"
{
	ParentBody "LMC X-1"
	Class      "X"
	MassSol    7
	Orbit
	{
		Period          0.0115857306
		SemiMajorAxis   0.15000000
		ArgOfPericenter 0
		MeanAnomaly     0
	}
	AccretionDisk
	{
		Brightness 1.0
	}
}

Star "R148"
{
	ParentBody "LMC X-1"
	Class      "O7 III" 				//maybe main sequence star?
	AbsMagn    -4.71
	MassSol	   35
	Orbit
	{
		Period          0.0115857306
		SemiMajorAxis   0.03000000
		ArgOfPericenter 180
		MeanAnomaly     0
	} 
}

Star "A 0620-00 A/Nova Mon 1975"
{
	ParentBody "A 0620-00"
	Class      "X"
	MassSol    10.8
	Orbit
	{
		Period          0.0008849772
		SemiMajorAxis   0.00108772
		ArgOfPericenter 0
		MeanAnomaly     0
	}
	AccretionDisk
	{
		Brightness 1.0
	}
}

Star "V616 Mon"
{
	ParentBody "A 0620-00"
	Class      "K4 V"
	AbsMagn    8.41
	MassSol	   0.6
	Orbit
	{
		Period          0.0008849772
		SemiMajorAxis   0.01957895
		ArgOfPericenter 180
		MeanAnomaly     0
	} 
}

Star "GRS 1009-45 A/Nova Vel 1993"
{
	ParentBody "GRS 1009-45"
	Class      "X"
	MassSol    4.2
	Orbit
	{
		Period          0.0007813813
		SemiMajorAxis   0.00175000
		ArgOfPericenter 0
		MeanAnomaly     0
	}
	AccretionDisk
	{
		Brightness 1.0
	}
}

Star "GRS 1009-45 B"
{
	ParentBody "GRS 1009-45"
	Class      "K8 V"
	MassSol	   0.6  
	Orbit
	{
		Period          0.0007813813
		SemiMajorAxis   0.01225000
		ArgOfPericenter 180
		MeanAnomaly     0
	} 
}

Star "GRS 1124-683 A/Nova Mus 1991"
{
	ParentBody "GRS 1124-683"
	Class      "X"
	MassSol    7.3
	Orbit
	{
		Period          0.0011852169
		SemiMajorAxis   0.00273092
		ArgOfPericenter 0
		MeanAnomaly     0
	}
	AccretionDisk
	{
		Brightness 1.0
	}
}

Star "GU Mus"
{
	ParentBody "GRS 1124-683"
	Class      "K5 V"
	AbsMagn    10.18
	MassSol	   1
	Orbit
	{
		Period          0.0011852169
		SemiMajorAxis   0.01993574
		ArgOfPericenter 180
		MeanAnomaly     0
	} 
}


Star "4U 1543-47 A"
{
	ParentBody "4U 1543-47"
	Class      "X"
	MassSol    9.4
	Orbit
	{
		Period          0.0030586530
		SemiMajorAxis   0.01071074
		ArgOfPericenter 0
		MeanAnomaly     0
	}
	AccretionDisk
	{
		Brightness 1.0
	}
}

Star "IL Lup"
{
	ParentBody "4U 1543-47"
	Class      "A2 V"
	AbsMagn    4.00
	MassSol	   2.7
	Orbit
	{
		Period          0.0030586530
		SemiMajorAxis   0.03728926
		ArgOfPericenter 180
		MeanAnomaly     0
	} 
}


Star "XTE J1550-564 A"
{
	ParentBody "XTE J1550-564"
	Class      "X"
	MassSol    9.6
	Orbit
	{
		Period          0.0042287671
		SemiMajorAxis   0.00491429
		ArgOfPericenter 0
		MeanAnomaly     0
	}
	AccretionDisk
	{
		Brightness 1.0
	}
}

Star "V381 Nor"
{
	ParentBody "XTE J1550-564"
	Class      "K3 III"
	Radius     6000000
	AbsMagn    3.92
	MassSol	   0.9
	Orbit
	{
		Period          0.0042287671
		SemiMajorAxis   0.05241905
		ArgOfPericenter 180
		MeanAnomaly     0
	} 
}

Star "Nor X-1 A/4U 1630-47"
{
	ParentBody "Nor X-1"
	Class      "X"
	Orbit
	{
		Period          0.005 		//generic
		SemiMajorAxis   0.00082305 	//generic
		ArgOfPericenter 0
		MeanAnomaly     0
	}
	AccretionDisk
	{
		Brightness 1.0
	}
}

Star "Nor X-1 B"
{
	ParentBody "Nor X-1"
	Class      "M5 V" 				//unknown
	Orbit
	{
		Period          0.005 		//generic
		SemiMajorAxis   0.006584362	//generic
		ArgOfPericenter 180
		MeanAnomaly     0
	} 
}

Star "GRO J1655-40 A/Nova Sco 1994"
{
	ParentBody "GRO J1655-40"
	Class      "X"
	MassSol    6.3
	Orbit
	{
		Period          0.0071833333
		SemiMajorAxis   0.02022989
		ArgOfPericenter 0
		MeanAnomaly     0
	}
	AccretionDisk
	{
		Brightness 1.0
	}
}

Star "V1033 Sco"
{
	ParentBody "GRO J1655-40"
	Class      "F4IV"
	AbsMagn    1.97
	MassSol	   2.4
	Orbit
	{
		Period          0.0071833333
		SemiMajorAxis   0.05310345
		ArgOfPericenter 180
		MeanAnomaly     0
	} 
}

Star "GRS 1659-487 A/GX 339-4"
{
	ParentBody "GRS 1659-487"
	Class      "X"
	MassSol    5.8
	Orbit
	{
		Period          0.0016894977
		SemiMajorAxis   0.00042938
		ArgOfPericenter 0
		MeanAnomaly     0
	}
	AccretionDisk
	{
		Brightness 1.0
	}
}

Star "V821 Ara"
{
	ParentBody "GRS 1659-487"
	Class      "G2 III"
	Orbit
	{
		Period          0.0016894977
		SemiMajorAxis   0.02490395
		ArgOfPericenter 180
		MeanAnomaly     0
	} 
}


Star "H 1705-250 A/Nova Oph 1977"
{
	ParentBody "H 1705-250"
	Class      "X"
	MassSol    7.0
	Orbit
	{
		Period          0.0014269406
		SemiMajorAxis   0.00101370
		ArgOfPericenter 0
		MeanAnomaly     0
	}
	AccretionDisk
	{
		Brightness 1.0
	}
}

Star "V2107 Oph"
{
	ParentBody "H 1705-250"
	Class      "K5 V"
	AbsMagn    5.97
	MassSol	   0.3  
	Orbit
	{
		Period          0.0014269406
		SemiMajorAxis   0.02365297 
		ArgOfPericenter 180
		MeanAnomaly     0
	} 
}


Star "GRS 1716-249 A"
{
	ParentBody "GRS 1716-249"
	Class      "X"				
	Orbit							
	{
		Period          0.005 		//generic
		SemiMajorAxis   0.00008230 	//generic
		ArgOfPericenter 0
		MeanAnomaly     0
	}
	AccretionDisk
	{
		Brightness 1.0
	}
}

Star "V2293 Oph"
{
	ParentBody "GRS 1716-249"
	Class      "M5 V"
	Orbit
	{
		Period          0.005 		//generic
		SemiMajorAxis   0.00658436 	//generic
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


Star "EXS 1737.9-2952 A"
{
	ParentBody "EXS 1737.9-2952"
	Class      "X"
	Orbit
	{
		Period          0.005 		//generic
		SemiMajorAxis   0.00008230 	//generic
		ArgOfPericenter 0
		MeanAnomaly     0
	}
	AccretionDisk
	{
		Brightness 1.0
	}
}

Star "EXS 1737.9-2952 B"
{
	ParentBody "EXS 1737.9-2952"
	Class      "M5 V" 				//generic
	Orbit
	{
		Period          0.005 		//generic
		SemiMajorAxis   0.00658436 	//generic
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


Star "GRS 1739-278 A"
{
	ParentBody "GRS 1739-278"
	Class      "X"
	Orbit
	{
		Period          0.005 			//generic
		SemiMajorAxis   0.00008230
		ArgOfPericenter 0
		MeanAnomaly     0
	}
	AccretionDisk
	{
		Brightness 1.0
	}
}

Star "GRS 1739-278 B"
{
	ParentBody "GRS 1739-278"
	Class      "F5 V"
	AbsMagn    8.24
	Orbit
	{
		Period          0.005 			//generic
		SemiMajorAxis   0.00658436
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "1E 1740.7-2942 A"
{
	ParentBody 	"1E 1740.7-2942"
	Class 		"X"
	Orbit
	{
		Period          0.0347945205
		SemiMajorAxis   0.00388350
		ArgOfPericenter 0
		MeanAnomaly 	0
	}
	AccretionDisk
	{
		Brightness 1.0
	}
}

Star "1E 1740.7-2942 B"
{
	ParentBody 	"1E 1740.7-2942"
	Class 		"M5 V" 					//unknown
	Orbit
	{
		Period          0.0347945205
		SemiMajorAxis   0.12944984
		ArgOfPericenter 180
		MeanAnomaly 	0
	} 
}



Star "GRS 1758-258 A"
{
	ParentBody "GRS 1758-258"
	Class 	   "X"
	Orbit
	{
		Period          0.0505479452
		SemiMajorAxis   0.00246914
		ArgOfPericenter 0
		MeanAnomaly 	0
	}
	AccretionDisk
	{
		Brightness 1.0
	}
}

Star "GRS 1758-258 B"
{
	ParentBody "GRS 1758-258"
	Class      "K V" 					//Class unknown
	Orbit
	{
		Period          0.0505479452
		SemiMajorAxis   0.19753086
		ArgOfPericenter 180
		MeanAnomaly 	0
	} 
}


Star "XTE J1819-254 A"
{
	ParentBody "XTE J1819-254"
	Class      "X"
	MassSol    7.1
	Orbit
	{
		Period          0.0077186301
		SemiMajorAxis   0.02633987	
		ArgOfPericenter 0
		MeanAnomaly 	0
	}
	AccretionDisk
	{
		Brightness 1.0
	}
}

Star "V4641 Sgr"
{
	ParentBody "XTE J1819-254"
	Class      "B9 III"
	MassSol	   3.1
	Orbit
	{
		Period          0.0077186301
		SemiMajorAxis   0.06032680
		ArgOfPericenter 180
		MeanAnomaly     0
	} 
}

Star "GRS 1915+105 A/XN Aql 1992"
{
	ParentBody "GRS 1915+105"
	Class      "X"
	MassSol    14
	Orbit
	{
		Period          0.0936073059
		SemiMajorAxis   0.04967742
		ArgOfPericenter 0
		MeanAnomaly     0
	}
	AccretionDisk
	{
		Brightness 1.0
	}
}

Star "V1487 Aql"
{
	ParentBody "GRS 1915+105"
	Class      "K III"
	MassSol	   1.5
	Orbit
	{
		Period          0.0936073059
		SemiMajorAxis   0.46365591
		ArgOfPericenter 180
		MeanAnomaly     0
	} 
}

Star "Cygnus X-1 A" 
{
	ParentBody "Cygnus X-1"
	Class      "X"
	MassSol    16
	Orbit
	{
		Period          0.0153420091
		SemiMajorAxis   0.14510638
		ArgOfPericenter 0
		MeanAnomaly     0
	}
	AccretionDisk
	{
		Brightness 1.0
	}
}

Star "HDE 226868/HIP 98298/SAO 69181/V1357 Cyg/AG+35 1910"
{
	ParentBody "Cygnus X-1"
	Class      "O9.7 Ia"
	AppMagn	   8.95
	Radius	   11136000
	MassSol	   31
	Orbit
	{
		Period          0.0153420091
		SemiMajorAxis   0.07489362
		ArgOfPericenter 180
		MeanAnomaly     0
	} 
}

Star "GS 2000+250 A/Nova Vul 1988"
{
	ParentBody "GS 2000+250"
	Class      "X"
	MassSol    7.5
	Orbit
	{
		Period          0.0009427169
		SemiMajorAxis   0.00074359
		ArgOfPericenter 0
		MeanAnomaly     0
	}
	AccretionDisk
	{
		Brightness 1.0
	}
}

Star "QZ Vul"
{
	ParentBody "GS 2000+250"
	Class      "K5 V"
	AbsMagn    9.50
	MassSol	   0.3
	Orbit
	{
		Period          0.0009427169
		SemiMajorAxis   0.01858974
		ArgOfPericenter 180
		MeanAnomaly     0
	} 
}


Star "GS 2023+338 A"
{
	ParentBody "GS 2023+338"
	Class      "X"
	MassSol    11.7
	Orbit
	{
		Period          0.0177294521
		SemiMajorAxis   0.00747967
		ArgOfPericenter 0
		MeanAnomaly     0
	}
	AccretionDisk
	{
		Brightness 1.0
	}
}

Star "V404 Cyg"
{
	ParentBody "GS 2023+338"
	Class      "K0 III"
	AbsMagn     0.75
	MassSol	    0.6
	Orbit
	{
		Period          0.0177294521
		SemiMajorAxis   0.14585366
		ArgOfPericenter 180
		MeanAnomaly     0
	} 
}

///////////////////////////////////////////////////////////
//          Isolated stellar mass black holes            //
///////////////////////////////////////////////////////////

Star "SN 1997D"
{
	ParentBody "SN 1997D bar"
	Class 	   "X"
	MassSol    3
	NoAccretionDisk true
}

//No accretion disks, discovered by micro-lensing 

Star "MACHO-98-BLG-6"
{
	ParentBody "MACHO-98-BLG-6 bar"
	Class 	   "X"
	MassSol    6
	NoAccretionDisk true
}

Star "MACHO-96-BLG-5"
{
	ParentBody "MACHO-96-BLG-5 bar"
	Class 	   "X"
	MassSol    6
	NoAccretionDisk true
}

Star "MACHO-99-BLG-22"
{
	ParentBody "MACHO-99-BLG-22 bar"
	Class 	   "X"
	MassSol    50
	NoAccretionDisk true
}

///////////////////////////////////////////////////////////
//               Sagittarius A* system                   //
//                                                       //
// MONITORING STELLAR ORBITS AROUND THE MASSIVE BLACK    //
// HOLE IN THE GALACTIC CENTER                           //
// S. Gillessen, F. Eisenhauer, S. Trippe, T. Alexander, //
// R. Genzel1, F. Martins, T. Ott1                       //
// Draft version October 26, 2008                        //
///////////////////////////////////////////////////////////

Star	"Sagittarius A*/Milky Way Central Black Hole"
{
	ParentBody  "Sgr A*"
	Class       "X"
	MassSol     4.31e6

	Obliquity	16	// orientation of accretion disk
	EqAscNode   64
/*
	// "Interstellar"-style disk
	AccretionDisk
	{
		Radius        1.65	// AU
		Temperature   3000
		Luminosity    1000
		Density       8
		Brightness    1
		TwistMagn     60
	}
*/
	// Realistic disk
	AccretionDisk
	{
		Radius        40	// AU
		AccretionRate 3.0e-5
		Temperature   14000 //91600
		Luminosity    10000
		Density       1000
		Brightness    1
		TwistMagn     60
	}	
}

Star "S1/S0-1"
{
	ParentBody "Sgr A*"
	Class      "B V"
	Orbit
	{
		Period			132
		SemiMajorAxis   4231.64
		Eccentricity    0.496
		Inclination     120.82
		AscendingNode 	341.61
		ArgOfPericenter 115.3
		Epoch 			2451891.9875
		MeanAnomaly     0
	}
}

Star "S2/S0-2"
{
	ParentBody "Sgr A*"
	Class      "B1 V" 				//confirmed
	MassSol    15
	Orbit
	{
		Period        	15.8
		SemiMajorAxis 	1024.59
		Eccentricity 	0.88
		Inclination 	135.25
		AscendingNode 	225.39
		ArgOfPericenter 63.56
		Epoch 			2452392.38
		MeanAnomaly     0
	}
}

Star "S4"
{
	ParentBody "Sgr A*"
	Class      "B V"
	Orbit
	{
		Period         	59.5
		SemiMajorAxis  	2482.34
		Eccentricity   	0.406
		Inclination    	77.83
		AscendingNode   258.11
		ArgOfPericenter 316.4
		Epoch 			2442194.6
		MeanAnomaly     0
	}
}

Star "S5"
{
	ParentBody "Sgr A*"
	Class      "B V"
	Orbit
	{
		Period          45.7
		SemiMajorAxis   2082.5
		Eccentricity    0.842
		Inclination     143.7
		AscendingNode   109
		ArgOfPericenter 236.3
		Epoch           2445554.9
		MeanAnomaly     0
	}
}

Star "S6"
{
	ParentBody "Sgr A*"
	Class      "B V"
	Orbit
	{
		Period          105
		SemiMajorAxis   3631.88
		Eccentricity    0.886
		Inclination     86.44
		AscendingNode   83.46
		ArgOfPericenter 129.5
		Epoch           2474555.75
		MeanAnomaly     0
	}
}


Star "S8/S0-4"
{
	ParentBody "Sgr A*"
	Class      "B V"
	Orbit
	{
		Period          96.1
		SemiMajorAxis   3423.63
		Eccentricity    0.824
		Inclination     74.01
		AscendingNode   315.9
		ArgOfPericenter 345.2
		Epoch           2445627.95
		MeanAnomaly     0
	}
}

Star "S9"
{
	ParentBody "Sgr A*"
	Class      "B V"
	Orbit
	{
		Period          58
		SemiMajorAxis   2440.69
		Eccentricity    0.825
		Inclination     81
		AscendingNode   147.58
		ArgOfPericenter 225.2
		Epoch           2447088.95
		MeanAnomaly     0
	}
}

Star "S12/S0-19"
{
	ParentBody "Sgr A*"
	Class      "B V"
	Orbit
	{
		Period          62.5
		SemiMajorAxis   2565.64
		Eccentricity    0.9
		Inclination     31.61
		AscendingNode   240.4
		ArgOfPericenter 308.8
		Epoch           2449948.8575
		MeanAnomaly     0
	}
}

Star "S13/S0-20"
{
	ParentBody "Sgr A*"
	Class      "B V"
	Orbit
	{
		Period          59.2
		SemiMajorAxis   2474.01
		Eccentricity    0.49
		Inclination     25.5
		AscendingNode   73.1
		ArgOfPericenter 248.2
		Epoch           2453334.725
		MeanAnomaly     0
	}
}

Star "S14/S0-16"
{
	ParentBody "Sgr A*"
	Class      "B V"
	Orbit
	{
		Period          47.3
		SemiMajorAxis   2132.48
		Eccentricity    0.963
		Inclination     99.4
		AscendingNode   227.74
		ArgOfPericenter 339
		Epoch           2451570.5675
		MeanAnomaly     0
	}
}

Star "S17"
{
	ParentBody "Sgr A*"
	Class      "K III" 					//unknown, late type star
	Orbit
	{
		Period          63.2
		SemiMajorAxis   2590.63
		Eccentricity    0.364
		Inclination     96.44
		AscendingNode   188.06
		ArgOfPericenter 319.45
		Epoch           2448623
		MeanAnomaly     0
	}
}

Star "S18"
{
	ParentBody "Sgr A*"
	Class      "B V"
	Orbit
	{
		Period          50
		SemiMajorAxis   2207.45
		Eccentricity    0.759
		Inclination     96.44
		AscendingNode   215.2
		ArgOfPericenter 151.7
		Epoch           2450084
		MeanAnomaly     0
	}
}

Star "S19"
{
	ParentBody "Sgr A*"
	Class      "B V"
	Orbit
	{
		Period          260
		SemiMajorAxis   6647.34
		Eccentricity    0.844
		Inclination     73.58
		AscendingNode   342.9
		ArgOfPericenter 153.3
		Epoch           2453407.775
		MeanAnomaly     0
	}
}

Star "S21"
{
	ParentBody "Sgr A*"
	Class      "K III" 				//unknown late sequence star
	Orbit
	{
		Period          35.8
		SemiMajorAxis   1774.29
		Eccentricity    0.784
		Inclination     54.8
		AscendingNode   252.7
		ArgOfPericenter 182.6
		Epoch           2461808.525
		MeanAnomaly     0
	}
}

Star "S24"
{
	ParentBody "Sgr A*"
	Class      "K III" 				//unknown late sequence star
	Orbit
	{
		Period          398
		SemiMajorAxis   8829.8
		Eccentricity    0.933
		Inclination     106.3
		AscendingNode   4.2
		ArgOfPericenter 291.5
		Epoch           2460639.725
		MeanAnomaly     0
	}
}

Star "S27"
{
	ParentBody "Sgr A*"
	Class      "K III" 				//unknown late sequence star
	Orbit
	{
		Period          112
		SemiMajorAxis   3781.82
		Eccentricity    0.952
		Inclination     92.91
		AscendingNode   191.9
		ArgOfPericenter 308.2
		Epoch           2473350.425
		MeanAnomaly     0
	}
}

Star "S29"
{
	ParentBody "Sgr A*"
	Class      "B V"
	Orbit
	{
		Period          91
		SemiMajorAxis   3307.01
		Eccentricity    0.916
		Inclination     122
		AscendingNode   157.2
		ArgOfPericenter 343.3
		Epoch           2459215.25
		MeanAnomaly     0
	}
}

Star "S31"
{
	ParentBody "Sgr A*"
	Class      "B V"
	Orbit
	{
		Period          59.4
		SemiMajorAxis   2482.34
		Eccentricity    0.934
		Inclination     153.8
		AscendingNode   103
		ArgOfPericenter 314
		Epoch           2456585.45
		MeanAnomaly     0
	}
}

Star "S33"
{
	ParentBody "Sgr A*"
	Class      "B V"
	Orbit
	{
		Period          96
		SemiMajorAxis   3415.3
		Eccentricity    0.731
		Inclination     42.9
		AscendingNode   82.9
		ArgOfPericenter 328.1
		Epoch           2439820.475
		MeanAnomaly     0
	}
}


Star "S38"
{
	ParentBody "Sgr A*"
	Class      "K III" 				//unknown late sequence star
	Orbit
	{
		Period          18.9
		SemiMajorAxis   1157.87
		Eccentricity    0.802
		Inclination     166
		AscendingNode   286
		ArgOfPericenter 203
		Epoch           2452640.75
		MeanAnomaly     0
	}
}


Star "S66"
{
	ParentBody "Sgr A*"
	Class      "B V"
	Orbit
	{
		Period          486
		SemiMajorAxis   10079.3
		Eccentricity    0.178
		Inclination     135.4
		AscendingNode   96.8
		ArgOfPericenter 106
		Epoch           2371920.5
		MeanAnomaly     0
	}
}

Star "S67"
{
	ParentBody "Sgr A*"
	Class      "B V"
	Orbit
	{
		Period          419
		SemiMajorAxis   9121.35
		Eccentricity    0.368
		Inclination     139.9
		AscendingNode   106
		ArgOfPericenter 215.2
		Epoch           2340143.75
		MeanAnomaly     0
	}
}


Star "S71"
{
	ParentBody "Sgr A*"
	Class      "B V"
	Orbit
	{
		Period          399
		SemiMajorAxis   8838.13
		Eccentricity    0.844
		Inclination     76.3
		AscendingNode   34.6
		ArgOfPericenter 331.4
		Epoch           2322246.5
		MeanAnomaly     0
	}
}



Star "S83"
{
	ParentBody "Sgr A*"
	Class      "B V"
	Orbit
	{
		Period          1700
		SemiMajorAxis   23199.05
		Eccentricity    0.657
		Inclination     123.8
		AscendingNode   73.6
		ArgOfPericenter 197.2
		Epoch           2473825.25
		MeanAnomaly     0
	}
}



Star "S87"
{
	ParentBody "Sgr A*"
	Class      "B V"
	Orbit
	{
		Period          516
		SemiMajorAxis   10495.8
		Eccentricity    0.423
		Inclination     142.7
		AscendingNode   109.9
		ArgOfPericenter 41.5
		Epoch           2322611.75
		MeanAnomaly     0
	}
}



Star "S96"
{
	ParentBody "Sgr A*"
	Class      "B V"
	Orbit
	{
		Period          701
		SemiMajorAxis   12869.85
		Eccentricity    0.131
		Inclination     126.8
		AscendingNode   115.78
		ArgOfPericenter 231
		Epoch           2314211
		MeanAnomaly     0
	}
}

Star "S97"
{
	ParentBody "Sgr A*"
	Class      "B V"
	Orbit
	{
		Period          1180
		SemiMajorAxis   18209.38
		Eccentricity    0.302
		Inclination     114.6
		AscendingNode   107.72
		ArgOfPericenter 38
		Epoch           2515463.75
		MeanAnomaly     0
	}
}

Star	"S102/S0-102"
{
	ParentBody	"Sgr A*"
	Class		"A5V"
	Luminosity  26  // 1/16 of S2 star
	Orbit
	{
		Epoch			2009.5
		Period			11.5
		//SemiMajorAxis	?	// let SE calculate it automatically using Period
		Eccentricity	0.68
		Inclination		151
		AscendingNode	175
		ArgOfPericen	185
	}
}
