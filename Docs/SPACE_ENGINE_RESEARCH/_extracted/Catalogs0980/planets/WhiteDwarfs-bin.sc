// Star solver log level:
// 0 - do not log
// 1 - log errors and warnings only
// 2 - log everything
LogLevel    1

//////////////////////////////////////////////////////////////////////////////////////
////////////////////////WHITE DWARFS WITH NORMAL STARS COMPANIONS/////////////////////
//////////////////////////////////////////////////////////////////////////////////////

Star	"U Gem A"
{
	ParentBody	"U Gem"
	Class		"M6V"
	Luminosity  0.0006776
	MassSol     0.42

	Orbit
	{
		Period			0.00047725
		SemiMajorAxis	0.00534 // mass ratio * 0.0072 AU
		Eccentricity	0
		Inclination		69.7
		AscendingNode	25	// random
		ArgOfPericen	180	// random
		MeanAnomaly     0	// random
	}
}

Star	"U Gem B"
{
	ParentBody	"U Gem"
	Class		"DA"
	Luminosity  0.01287
	MassSol     1.2
	Radius      10000

	AccretionDisk
	{
		Radius        0.002838 // AU
		Temperature   30000
		Brightness    1.0
		Density       5000
	}

	Orbit
	{
		Period			0.00047725
		SemiMajorAxis	0.00187 // mass ratio * 0.0072 AU
		Eccentricity	0
		Inclination		69.7
		AscendingNode	25	// random
		ArgOfPericen	0	// random
		MeanAnomaly     0	// random
	}
}

//ZET Cyg;eng wiki

Star "ZET Cyg A/HIP 104732/HD 202109"
{
	ParentBody "ZET Cyg"
	Class      "G8 III"
	Radius     10440000
	AppMagn    3.26
	MassSol    3.05
	Orbit
	{
		Period          17.50410959
		SemiMajorAxis   1.37
		Eccentricity    0.22
		ArgOfPericenter 41
		Epoch           2440712
		MeanAnomaly     0
	}
}

Star "ZET Cyg B"
{
	ParentBody "ZET Cyg"
	Class      "DA"
	AppMagn    11.6
	MassSol    0.6
	Orbit
	{
		Period          17.50410959
		SemiMajorAxis   6.9643
		Eccentricity    0.22
		ArgOfPericenter 221
		Epoch           2440712
		MeanAnomaly     0
	}
}

//CI Cyg; sp wiki

Star "CI Cyg A/HIP 97594"
{
	ParentBody "CI Cyg"
	Class      "M5.5 II"
	AppMagn    11.9
	MassSol    0.5
	Orbit
	{
		Period          2.34315068
		SemiMajorAxis   0.8832
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "CI Cyg B"
{
	ParentBody "CI Cyg"
	Class      "DA"
	MassSol    0.5
	Orbit
	{
		Period          2.34315068
		SemiMajorAxis   0.8832
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//SS Cyg; sp and eng wiki

Star "SS Cyg A"
{
	ParentBody "SS Cyg"
	Class      "DA"
	MassSol    0.6
	Orbit
	{
		Period          0.00075342
		SemiMajorAxis   0.0033
		Inclination     50
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "SS Cyg B/HD 206697"
{
	ParentBody "SS Cyg"
	Class      "K4 V"
	AppMagn    12.2
	MassSol    0.4
	Orbit
	{
		Period          0.00075342
		SemiMajorAxis   0.005
		Inclination     50
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Barycenter "Regulus (AB)"
{
	ParentBody "Regulus"
	Orbit
	{
		Period          120301.8517
		SemiMajorAxis   823.5294
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Regulus A/ALF Leo A/HIP 49669/HD 87901"
{
	ParentBody "Regulus (AB)"
	Class      "B7 V"
	Radius     2164400
	AppMagn    1.35
	MassSol    3.8
	Orbit
	{
		Period          0.1096
		SemiMajorAxis   0.0269
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "ALF Leo B"
{
	ParentBody "Regulus (AB)"
	Class      "DA"
	MassSol    0.3
	Orbit
	{
		Period          0.1096
		SemiMajorAxis   0.3401
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Barycenter "ALF Leo (CD)"
{
	ParentBody "Regulus"
	Orbit
	{
		Period          120301.8517
		SemiMajorAxis   3376.4706
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "ALF Leo C"
{
	ParentBody "ALF Leo (CD)"
	Class      "K1 V"
	Radius     450000
	AppMagn    8.14
	MassSol    0.8
	Orbit
	{
		Period          5.4795
		SemiMajorAxis   20
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "ALF Leo D"
{
	ParentBody "ALF Leo (CD)"
	Class      "M5 V"
	AbsMagn    13.5
	MassSol    0.2
	Orbit
	{
		Period          5.4795
		SemiMajorAxis   80
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//CSS 41177;spanish wiki

Star "CSS 41177 A"
{
	ParentBody "CSS 41177"
	Class      "DA"
	Radius     14616
	AppMagn    17.3
	MassSol    0.28
	Orbit
	{
		Period          0.11583333
		SemiMajorAxis   0.0019
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "CSS 41177 B"
{
	ParentBody "CSS 41177"
	Class      "DA"
	Radius     12110.4
	MassSol    0.27
	Orbit
	{
		Period          0.11583333
		SemiMajorAxis   0.0019
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//G 21-15;spanish wiki

Barycenter "G 21-15 (AB)"
{
	ParentBody "G 21-15"
	Orbit
	{
		Period          146550.28
		SemiMajorAxis   1200
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "G 21-15 A"
{
	ParentBody "G 21-15 (AB)"
	Class      "DA4"
	AbsMagn    10.38
	MassSol    0.35
	Orbit
	{
		Period          0.01717808
		SemiMajorAxis   0.0413
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "G 21-15 B"
{
	ParentBody "G 21-15 (AB)"
	Class      "DA4"
	AbsMagn    12.15
	MassSol    0.6
	Orbit
	{
		Period          0.01717808
		SemiMajorAxis   0.0241
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "G 21-15 C"
{
	ParentBody "G 21-15"
	Class      "DC11"
	AbsMagn    15.3
	MassSol    0.57
	Orbit
	{
		Period          146550.28
		SemiMajorAxis   2000
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "G 261-43 A"
{
	ParentBody "G 261-43"
	Class      "DA3"
	AbsMagn    11.04
	MassSol    0.57
	Orbit
	{
		Period          136.2521
		SemiMajorAxis   17.6786
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "G 261-43 B"
{
	ParentBody "G 261-43"
	Class      "DA"
	AbsMagn    14.8
	MassSol    0.84
	Orbit
	{
		Period          136.2521
		SemiMajorAxis   11.9962
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//ZET Cap;6thCVB, spanish wiki
//using wiki semiaxis, more accurative with total mass known data

Star "ZET Cap A/HIP 105881/HD 204075"
{
	ParentBody "ZET Cap"
	Class      "G4 Ib"
	Radius     20184000
	AppMagn    3.75
	MassSol    4.5
	Orbit
	{
		Period          6.5156
		SemiMajorAxis   0.6
		Eccentricity    0.2821
		Inclination     111.7
		AscendingNode   190.7
		ArgOfPericenter 233.8
		Epoch           2445996
		MeanAnomaly     0
	}
}

Star "ZET Cap B"
{
	ParentBody "ZET Cap"
	Class      "DA2"
	Orbit
	{
		Period          6.5156
		SemiMajorAxis   5.4
		Eccentricity    0.2821
		Inclination     111.7
		AscendingNode   190.7
		ArgOfPericenter 53.8
		Epoch           2445996
		MeanAnomaly     0
	}
}

//AG Dra; spanish wiki

Star "AG Dra A/HIP 78512"
{
	ParentBody "AG Dra"
	Class      "K3 III"
	Radius     26448000
	AppMagn    9.75
	MassSol    1.5
	Orbit
	{
		Period          1.5178
		SemiMajorAxis   0.4857
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "AG Dra B"
{
	ParentBody "AG Dra"
	Class      "DA"
	Radius     41760
	MassSol    0.6
	Orbit
	{
		Period          1.5178
		SemiMajorAxis   1.2143
		Inclination     0
		AscendingNode   0
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//CM Dra; spanish and english wiki
//distance from spanish wiki, seems to be more recent (8 ly futher away)

Barycenter "CM Dra A"
{
	ParentBody "CM Dra"
	Orbit
	{
		Period          10656.16
		SemiMajorAxis   129.2308
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "CM Dra Aa/LHS 421/GJ 630.1 A"
{
	ParentBody "CM Dra A"
	Class      "M4 V"
 
	AppMagn    12.9
	MassSol    0.24
	Orbit
	{
		Period          0.00347945
		SemiMajorAxis   0.0082
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "CM Dra Ab"
{
	ParentBody "CM Dra A"
	Class      "M4 V"
 
	AppMagn    15
	MassSol    0.21
	Orbit
	{
		Period          0.00347945
		SemiMajorAxis   0.0093
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "CM Dra B/WD 1633+571/LHS 422/GJ 630.1 B"
{
	ParentBody "CM Dra" 
	Class      "DQ8"
	Orbit
	{
		Period          10656.16
		SemiMajorAxis   290.7692
		Inclination     0
		AscendingNode   0
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//Baltic Astronomy, vol. 19, 157–167, 2010
//CHEMICAL COMPOSITION OF THE RS CVn-TYPE STAR 29 DRACONIS
//G. Barisevicius, G. Tautvaisiene, S. Berdyugina, Y. Chorniy and I. Ilyin
//semiaxis corrected from 6thCVB with 3rd kepler law knowing system mass a period

Star "29 Dra A/HIP 85852/HD 160538"
{
	ParentBody "29 Dra"
	Class      "K0 III"
	Radius     2923200
	AppMagn    6.64
	MassSol    1.2
	Orbit
	{
		Period          2.4762
		SemiMajorAxis   0.6941
		Eccentricity    0.072
		Inclination     156.2
		AscendingNode   349.8
		ArgOfPericenter 297.5
		Epoch           2447479.67
		MeanAnomaly     0
	}
}

Star "29 Dra B"
{
	ParentBody "29 Dra"
	Class      "DA1"
	Radius     8352
	MassSol    0.55
	Orbit
	{
		Period          2.4762
		SemiMajorAxis   1.5143
		Eccentricity    0.072
		Inclination     156.2
		AscendingNode   349.8
		ArgOfPericenter 117.5
		Epoch           2447479.67
		MeanAnomaly     0
	}
}
//63 Eri;6thCVB, SIMBAD
//apparent semiaxis major in 6thCVB has no sense
//changed for a SA according to a tipical Mass of a G4 subgiant ~1 Ms
//and typical DA ~0.5 with observed period

Star "63 Eri A/HIP 23221/HD 32008"
{
	ParentBody "63 Eri"
	Class      "G4 IV"
	AppMagn    5.4
	Orbit
	{
		Period          2.474
		SemiMajorAxis   0.6988
		Eccentricity    0.3
		Inclination     109.5
		AscendingNode   40.9
		ArgOfPericenter 171
		Epoch           2450384
		MeanAnomaly     0
	}
}

Star "63 Eri B"
{
	ParentBody "63 Eri"
	Class      "DA"
	Orbit
	{
		Period          2.474
		SemiMajorAxis   1.3977
		Eccentricity    0.3
		Inclination     109.5
		AscendingNode   40.9
		ArgOfPericenter 351
		Epoch           2450384
		MeanAnomaly     0
	}
}

//Gliese 283; spanish wiki

Star "Gliese 283 A/WD 0738-172/LHS 235"
{
	ParentBody "Gliese 283"
	Class      "DAZ 6.6"
	AppMagn    12.9
	MassSol    0.62
	Orbit
	{
		Period          3013.90606465
		SemiMajorAxis   47.6059
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Gliese 283 B/LHS 234"
{
	ParentBody "Gliese 283"
	Class      "M6 V"
	AppMagn    16.4
	Orbit
	{
		Period          3013.90606465
		SemiMajorAxis   147.5782
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//WD 0751-252;spanish wiki

Star "WD 0751-252 A/SCR J0753-2524"
{
	ParentBody "WD 0751-252"
	Class      "DC"
	Orbit
	{
		Period          716151.876
		SemiMajorAxis   4000
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Ross 429/LTT 2976/NLTT 18618"
{
	ParentBody "WD 0751-252"
	Class      "K5 V"
	AppMagn    9.75
	Orbit
	{
		Period          716151.876
		SemiMajorAxis   4000
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//IOT Ari; eng an sp wiki

Star "IOT Ari A/HIP 9110/HD 11909"
{
	ParentBody "IOT Ari"
	Class      "K1 III"
	Radius     18792000
	AppMagn    5.11
	MassSol    3.5
	Orbit
	{
		Period          4.295
		SemiMajorAxis   0.1279
		Eccentricity    0.36
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "IOT Ari B"
{
	ParentBody "IOT Ari"
	Class      "DA"  //suspected white dwarf in eng wiki
	MassSol    0.1
	Orbit
	{
		Period          4.295
		SemiMajorAxis   4.4764
		Eccentricity    0.36
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//TT Ari;WD STAR PRESENT

Star "TT Ari A"
{
	ParentBody "TT Ari"
	Class      "DA"
	AppMagn    10.5
	Orbit
	{
		Period          0.00037685 //confirmed
		SemiMajorAxis   0.0022 //unknown
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

//TT Ari; spanish wiki

Star "TT Ari B"
{
	ParentBody "TT Ari"
	Class      "M3 V"
	Orbit
	{
		Period          0.00037685
		SemiMajorAxis   0.0022
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//AE Aqr;english wiki

Star "AE Aqr B"
{
	ParentBody "AE Aqr"
	Class      "K4 V"
	Radius     549840
	MassSol    0.37
	Orbit
	{
		Period          0.00112785
		SemiMajorAxis   0.00685868
		Inclination     70
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "AE Aqr A/HIP 101991"
{
	ParentBody "AE Aqr"
	Class      "DA"
	Radius     6960
	MassSol    0.63
	Orbit
	{
		Period          0.00112785
		SemiMajorAxis   0.00402811
		Inclination     70
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

//RS Oph;spanish wiki and the article:
//Spectroscopic Orbits and spectral variations of RS OPHIUCHI
//Authors: E. Brandi, C. Quiroga, O.E. Ferrer, J. Miko lajewska, L.G.Garcнa
//nova?

Star "RS Oph A/HD 162214"
{
	ParentBody "RS Oph"
	Class      "M2 III"
	MassSol    0.68
	AppMagn    11.55				//spanish wiki median
	Orbit
	{
		Period          1.2427
		SemiMajorAxis   0.7127
		Eccentricity    0.04
		Inclination     49
		ArgOfPericenter 87
		Epoch           2445722.37
		MeanAnomaly     0
	}
}

Star "RS Oph B"
{
	ParentBody "RS Oph"
	Class      "DA"
	MassSol    1.2
	Orbit
	{
		Period          1.2427
		SemiMajorAxis   0.4039
		Eccentricity    0.04
		Inclination     49
		ArgOfPericenter 267
		Epoch           2445722.37
		MeanAnomaly     0
	}
}

//14 Aur;spanish wiki, english wiki

Barycenter "14 Aur A"
{
	ParentBody "14 Aur"
	Orbit
	{
		Period          20000
		SemiMajorAxis   442.83454755
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Barycenter "14 Aur C"
{
	ParentBody "14 Aur"
	Orbit
	{
		Period          20000
		SemiMajorAxis   708.08569785
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "14 Aur Aa/HIP 24504/HD 33959"
{
	ParentBody "14 Aur A"
	Class      "A9 IV"
	Radius     3062400
	AbsMagn    2.69
	MassSol    2.4
	Orbit
	{
		Period          0.0104
		SemiMajorAxis   0.01666667
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "14 Aur Ab"
{
	ParentBody "14 Aur A"
	Class      "K V"
	Orbit
	{
		Period          0.0104
		SemiMajorAxis   0.05333333
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Barycenter "14 Aur (Ca)"
{
	ParentBody "14 Aur C"
	Orbit
	{
		Period          1600
		SemiMajorAxis   8.34604964
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "14 Aur Cb"
{
	ParentBody "14 Aur C"
	Class      "DO" 			//white dwarf confirmed
	MassSol    0.1   			//spectra related with temperature (40000 K)
	Orbit
	{
		Period          1600
		SemiMajorAxis   156.07112827
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "14 Aur Caa"
{
	ParentBody "14 Aur (Ca)"
	Class      "F3 V"
	Radius     1322400
	AbsMagn    3.3
	MassSol    1.87
	Orbit
	{
		Period          0.008186174
		SemiMajorAxis   0.01337
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "14 Aur Cab"
{
	ParentBody "14 Aur (Ca)"
	AbsMagn    7 				//unknown
	Orbit
	{
		Period          0.008186174
		SemiMajorAxis   0.03663
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//AB Cet;spanish wiki

Star "AB Cet A/HD 15144"
{
	ParentBody "AB Cet"
	Class      "A6 V"
	Radius     1134480
	AppMagn    5.86
	MassSol    1.84
	Orbit
	{
		Period          0.0082
		SemiMajorAxis   0.0138
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "AB Cet B"
{
	ParentBody "AB Cet"
	Class      "DA"
	MassSol    0.62
	Orbit
	{
		Period          0.0082
		SemiMajorAxis   0.0411
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//BD+85 63;6thCVB,spanish wiki

Star "BD+85 63 A/HIP 21386/HD 26367"
{
	ParentBody "BD+85 63"
	Class      "F7 V"
	AppMagn    6.57
	MassSol    1.27
	Orbit
	{
		Period          2.8219
		SemiMajorAxis   0.2389
		Eccentricity    0.2
		Inclination     99
		AscendingNode   203
		ArgOfPericenter 48
		Epoch           2449242
		MeanAnomaly     0
	}
}

Star "BD+85 63 B"
{
	ParentBody "BD+85 63"
	Class      "DA"
	MassSol    0.6
	Orbit
	{
		Period          2.8219
		SemiMajorAxis   0.5058
		Eccentricity    0.2
		Inclination     99
		AscendingNode   203
		ArgOfPericenter 228
		Epoch           2449242
		MeanAnomaly     0
	}
}

//IK Peg;english and spanish wiki

Star "IK Peg A/HIP 105860/HD 204188"
{
	ParentBody "IK Peg"
	Class      "A8 V"
	Radius     1113600
	AppMagn    6.078
	MassSol    1.65
	Orbit
	{
		Period          0.0595
		SemiMajorAxis   0.0863
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "IK Peg B/WD 2124+191/EUVE J2126+193"
{
	ParentBody "IK Peg"
	Class      "DA"
	Radius     6333.6
	MassSol    1.15
	Orbit
	{
		Period          0.0595
		SemiMajorAxis   0.1238
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//EPS2 Ara;6thCVB,english and spanish wiki

Star "EPS2 Ara A/HR 6314/GJ 3985/Gliese 3985/HIP 83431/HD 153580"
{
	ParentBody "EPS2 Ara"
	Class      "F5 V"
	AppMagn    5.44
	MassSol    1.44
	Orbit
	{
		Period          41.32
		SemiMajorAxis   5.0537
		Eccentricity    0.622
		Inclination     133.8
		AscendingNode   73.6
		ArgOfPericenter 94.1
		Epoch           2451343.650189
		MeanAnomaly     0
	}
}

Star "EPS2 Ara B/WD 1659-531/BPM 24602/GJ 2125/Gliese 2125"
{
	ParentBody "EPS2 Ara"
	Class      "DA"
	AppMagn    8.65
	MassSol    0.66
	Orbit
	{
		Period          41.32
		SemiMajorAxis   11.0263
		Eccentricity    0.622
		Inclination     133.8
		AscendingNode   73.6
		ArgOfPericenter 274.1
		Epoch           2451343.650189
		MeanAnomaly     0
	}
}

//EPS Aql;6thCVB,english,spanish wiki

//EPS Aql A is classified as barium star, because
//barium stars atmospheres are enriched with heavy metals
//from a nearby white dwarf star and the mass of the companion
//seems to be confirmed I classified B as white dwarf, but still
//it's a supposition.

Star "EPS Aql A/HIP 93244/HD 176411"
{
	ParentBody "EPS Aql"
	Class      "K1 III"
	Radius     7057440
	AppMagn    4.03
	MassSol    2.1
	Orbit
	{
		Period          3.4811
		SemiMajorAxis   0.1113
		Eccentricity    0.27
		Inclination     87.5
		AscendingNode   58.7
		ArgOfPericenter 82
		Epoch           2441718.5
		MeanAnomaly     0
	}
}

Star "EPS Aql B"
{
	ParentBody "EPS Aql"
	Class      "DA" 		//suspected
	MassSol    0.47
	Orbit
	{
		Period          3.4811
		SemiMajorAxis   0.4973
		Eccentricity    0.27
		Inclination     87.5
		AscendingNode   58.7
		ArgOfPericenter 262
		Epoch           2441718.5
		MeanAnomaly     0
	}
}

//BD+62 597;6thCVB,english
//contacting binary; symbiotic star

Star "BD+62 597 A/HIP 17296/HD 22649"
{
	ParentBody "BD+62 597"
	Class      "S3 III"
	Radius     45240000 	//half size
	AppMagn    5.12
	Orbit
	{
		Period          1.6334
		SemiMajorAxis   0.0734
		Eccentricity    0.088
		Inclination     105.6
		AscendingNode   162.1
		ArgOfPericenter 334.3
		Epoch           2442794.5
		MeanAnomaly     0
	}
}

Star "BD+62 597 B"
{
	ParentBody "BD+62 597"
	Class      "DA" 		//unknown, UV bursts
	Orbit
	{
		Period          1.6334
		SemiMajorAxis   0.2935
		Eccentricity    0.088
		Inclination     105.6
		AscendingNode   162.1
		ArgOfPericenter 154.3
		Epoch           2442794.5
		MeanAnomaly     0
	}
}

//Stein 2051;english wiki
//Triple system Stein 2051 (G175-34)
//The Astronomical Journal. Sept 1977
//K. Aa. Strand

Star "Stein 2051 A/LHS 26/HIP 21088"
{
	ParentBody "Stein 2051"
	Class      "M4 V"
	AppMagn    12.4
	MassSol    0.25
	Orbit
	{
		Period          297.2173
		SemiMajorAxis   26.9607
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Stein 2051 B/LHS 27/WD 0426+58/AC+58 2500/EGGR 180/NLTT 13375"
{
	ParentBody "Stein 2051"
	Class      "DC5"
	MassSol    0.5
	Orbit
	{
		Period          297.2173
		SemiMajorAxis   13.4804
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//Z Cam;spanish wiki

Star "Z Cam A"
{
	ParentBody "Z Cam"
	Class      "G1 III"
	AppMagn    10
	Orbit
	{
		Period          0.0008
		SemiMajorAxis   0.0019
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Z Cam B"
{
	ParentBody "Z Cam"
	Class      "DA"
	Orbit
	{
		Period          0.0008
		SemiMajorAxis   0.0116
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//T CrB;english, spanish wiki
//recurrent nova

Star "T CrB A/HD 143454"
{
	ParentBody "T CrB"
	Class      "M3 III"
	AppMagn    10.8
	MassSol    1.12
	Orbit
	{
		Period          0.623053
		SemiMajorAxis   0.543946
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "T CrB B"
{
	ParentBody "T CrB"
	Class      "DA"
	MassSol    1.37
	Orbit
	{
		Period          0.623053
		SemiMajorAxis   0.444686
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//////////////////////////////////////////////////////////////////////////////////////
////////////////////////WHITE DWARFS WITH BROWN DWARFS COMPANIONS/////////////////////
//////////////////////////////////////////////////////////////////////////////////////

Star "SDSS 00390-00300 A/SDSS J003902.47-003000.3 A"
{
	ParentBody "SDSS 00390-00300"
	Class      "DA"
	Orbit 
	{
		Period          13993.7904    //Generic
		SemiMajorAxis   63.8757
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "SDSS 00390-00300 B/SDSS J003902.47-003000.3 B"           //Unconfirmed
{
	ParentBody "SDSS 00390-00300"
	Class      "L0 V"
	MassSol    0.07602
	DiscDate   "2011"
	Orbit
	{
		Period          13993.7904    //Generic
		SemiMajorAxis   420.1243
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "SDSS 01353+14455 A/SDSS J013532.98+144555.8 A"
{
	ParentBody "SDSS 01353+14455"
	Class      "DA"
	Orbit 
	{
		Period          0.000194
		SemiMajorAxis   0.0005
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "SDSS 01353+14455 B/SDSS J013532.98+144555.8 B"
{
	ParentBody "SDSS 01353+14455"
	Class      "L5 V"
	MassSol    0.0551145
	DiscDate   "2011"
	Orbit
	{
		Period          0.000194
		SemiMajorAxis   0.0048
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "HK Cet A/PHL 1159/GD 1400/MCT 0145-2211/WD 0145-221/2MASS J01472183-2156512 A"
{
	ParentBody "HK Cet"
	Class      "DA 4.1"
	AppMagn    14.85
	Orbit 
	{
		Period          0.0011
		SemiMajorAxis   1.2283
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "HK Ceti B/2MASS J01472183-2156512 B"
{
	ParentBody "HK Cet"
	Class      "L6 V"
	AppMagnJ   17.5
	AppMagnH   15.42
	AppMagnKs  15.1
	Teff       1650
	MassSol    0.057015
	DiscDate   "2004"
	Orbit
	{
		Period          0.0011
		SemiMajorAxis   10.7717
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "SDSS 09075+05364 A"
{
	ParentBody "SDSS 09075+05364"
	Class      "DA"
	Orbit 
	{
		Period          19675.315    //Generic
		SemiMajorAxis   59.571
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "SDSS 09075+05364 B/SDSS J090759.59+053649.7 B"           //Unconfirmed
{
	ParentBody "SDSS 09075+05364"
	Class      "L4 V"
	MassSol    0.0551145
	DiscDate   "2011"
	Orbit
	{
		Period          19675.315    //Generic
		SemiMajorAxis   540.429
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "SDSS 10025+09395 A"
{
	ParentBody "SDSS 10025+09395"
	Class      "DA"
	Orbit 
	{
		Period          11330.7561    //Generic
		SemiMajorAxis   54.2225
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "SDSS 10025+09395 B/SDSS J100259.88+093950.0 B"           //Unconfirmed
{
	ParentBody "SDSS 10025+09395"
	Class      "L0 V"
	MassSol    0.0741195
	DiscDate   "2011"
	Orbit
	{
		Period          11330.7561    //Generic
		SemiMajorAxis   365.7775
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "SDSS 10344+00520 A/WD 1032+011/SDSS J103448.92+005201.4 A"
{
	ParentBody "SDSS 10344+00520"
	Class      "DA3"
	Orbit 
	{
		Period          2465.7539    //Generic
		SemiMajorAxis   14.1953
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "SDSS 10344+00520 B/SDSS J103448.92+005201.4 B"           //Unconfirmed
{
	ParentBody "SDSS 10344+00520"
	Class      "L5 V"
	MassSol    0.05226375
	DiscDate   "2011"
	Orbit
	{
		Period          2465.7539    //Generic
		SemiMajorAxis   135.8047
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "V379 Vir A"
{
	ParentBody "V379 Vir"
	Class      "DAH"
	Orbit 
	{
		Period          0.0002    //Generic
		SemiMajorAxis   0.0003
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "SDSS 12120+01362 B/SDSS J121209.31+013627.7 B"
{
	ParentBody "V379 Vir"
	Class      "L8 V"
	MassSol    0.0475125
	DiscDate   "2008"
	Orbit
	{
		Period          0.0002    //Generic
		SemiMajorAxis   0.0027
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "WD 1317+021 A/LEDA 46495/UH 566/SDSS J131955.04+015259.5 A"
{
	ParentBody "WD 1317+021"
	Class      "DA3"
	Orbit 
	{
		Period          1962.5943    //Generic
		SemiMajorAxis   15.456
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "WD 1317+021 B/SDSS 13195+01525 B/SDSS J131955.04+015259.5 B"           //Unconfirmed
{
	ParentBody "WD 1317+021"
	Class      "L1 V"
	MassSol    0.06746775
	DiscDate   "2011"
	Orbit
	{
		Period          1962.5943    //Generic
		SemiMajorAxis   114.544
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "GD 165 A/CX Boo/L 1124-10/NLTT 37242/LTT 14236/1SWASP J142439.16+091714.2/WD 1422+095"
{
	ParentBody "GD 165"
	Class      "DA"
	AppMagn    14.32
	Orbit 
	{
		Period          1734.7534    //Generic
		SemiMajorAxis   14.9706
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "GD 165 B/2MASS J14243909+0917104 B"
{
	ParentBody "GD 165"
	Class      "L4 V"
	AppMagnI   19.16
	AppMagnJ   15.69
	AppMagnH   14.78
	AppMagnKs  14.17
	AppMagnW1  13.21
	AppMagnW2  13.01
	AppMagnW3  12.47
	Teff       1900
	Radius     60123.46
	MassSol    0.07126875
	DiscDate   "1988"
	Orbit
	{
		Period          1734.7534    //Generic
		SemiMajorAxis   105.0294
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "SDSS 15443+06010 A/SDSS J154431.47+060104.3 A"
{
	ParentBody "SDSS 15443+06010"
	Class      "DA"
	AppMagn    15
	Orbit 
	{
		Period          3542.6826    //Generic
		SemiMajorAxis   15.275
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "SDSS 15443+06010 B/SDSS J154431.47+060104.3 B"           //Unconfirmed
{
	ParentBody "SDSS 15443+06010"
	Class      "T3 V"
	MassSol    0.0437115
	DiscDate   "2011"
	Orbit
	{
		Period          3542.6826    //Generic
		SemiMajorAxis   174.725
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "PHL 5038 A"
{
	ParentBody "PHL 5038"
	Class      "DA"
	Orbit 
	{
		Period          550.3137    //Generic
		SemiMajorAxis   4.6855
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "PHL 5038 B/SDSS 22203-00410 B/SDSS J222030.68-004107.9 B/2MASS J22203068-0041070 B"
{
	ParentBody "PHL 5038"
	Class      "L8 V"
	AppMagnH   17.84
	AppMagnKs  17.18
	Teff       1450
	MassSol    0.04656225
	DiscDate   "2009"
	Orbit
	{
		Period          550.3137    //Generic
		SemiMajorAxis   50.3145
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "SDSS 22084-00051 A"
{
	ParentBody "SDSS 22084-00051"
	Class      "DA"
	Orbit 
	{
		Period          6539.0176    //Generic
		SemiMajorAxis   34.4789
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "SDSS 22084-00051 B/SDSS J220841.63-000514.5 B"           //Unconfirmed
{
	ParentBody "SDSS 22084-00051"
	Class      "L1 V"
	MassSol    0.06746775
	DiscDate   "2011"
	Orbit
	{
		Period          6539.0176    //Generic
		SemiMajorAxis   255.5211
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "SDSS 22255+00163 A"
{
	ParentBody "SDSS 22255+00163"
	Class      "DA"
	Orbit 
	{
		Period          8849.6222    //Generic
		SemiMajorAxis   28.6997
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "SDSS 22255+00163 B/SDSS J222551.65+001637.7 B"           //Unconfirmed
{
	ParentBody "SDSS 22255+00163"
	Class      "L7 V"
	MassSol    0.04466175
	DiscDate   "2011"
	Orbit
	{
		Period          8849.6222    //Generic
		SemiMajorAxis   321.3003
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "WZ Sge A/Nova Sge 1946/WD 2003+17/EGGR 136"
{
	ParentBody "WZ Sge"
	Class      "DA"
	AppMagn    15.2
	Orbit 
	{
		Period          0.000155
		SemiMajorAxis   0.0003
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "WZ Sge B/2MASS J20073649+1742147 B"
{
	ParentBody "WZ Sge"
	Class      "L2 V"
	Teff       1900
	MassSol    0.0665175
	DiscDate   "2013"
	Orbit
	{
		Period          0.000155
		SemiMajorAxis   0.0026
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//SX LMi;english wiki
//Dwarf Nova

Star "SX LMi A"
{
	ParentBody "SX LMi"
	Class      "DA"
	AppMagn    16.8
	MassSol    1
	Orbit
	{
		Period          0.000184
		SemiMajorAxis   0.000332
		ArgOfPericenter 0
		MeanAnomaly     0
	}
	AccretionDisk
	{
		Brightness		1.0
	}
}

Star "SX LMi B"
{
	ParentBody "SX LMi"
	Class      "M5 V"	//suspected
	MassSol    0.11
	Orbit
	{
		Period          0.000184
		SemiMajorAxis   0.003022
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//AM CVn;english wiki

Star "AM CVn A"
{
	ParentBody "AM CVn"
	Class      "DQ"
	AppMagn    13.7
	MassSol    0.71
	Orbit
	{
		Period          0.000033
		SemiMajorAxis   0.000149
		Inclination     43
		ArgOfPericenter 0
		MeanAnomaly     0
	}
	AccretionDisk
	{
		Brightness		1.0
	}
}

Star "AM CVn B"
{
	ParentBody "AM CVn"
	Class      "DB"
	AppMagn    14.2
	MassSol    0.13
	Orbit
	{
		Period          0.000033
		SemiMajorAxis   0.000814
		Inclination     43
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}
//SSDS 09170+46382;spanish wiki

Star "SSDS 09170+46382 A"
{
	ParentBody "SSDS 09170+46382"
	Class      "DA"
	Radius     55680
	MassSol    0.17
	Orbit
	{
		Period          0.000868
		SemiMajorAxis   0.004356
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "SSDS 09170+46382 B"
{
	ParentBody "SSDS 09170+46382"
	Class      "DA"
	MassSol    0.28
	Orbit
	{
		Period          0.000868
		SemiMajorAxis   0.002644
		ArgOfPericenter 180
		MeanAnomaly     0
	}
	AccretionDisk
	{
		Brightness		1.0
	}
}
