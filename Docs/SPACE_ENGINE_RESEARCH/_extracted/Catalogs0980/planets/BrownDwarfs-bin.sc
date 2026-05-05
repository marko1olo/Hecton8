//////////////////////////////////////////////////////////////////
//					BINARY/MULTIPLE BROWN DWARFS				//
///         Catalogue from Professor Wm. Robert Johnston        //
//   http://www.johnstonsarchive.net/astro/browndwarflist.html  //
//                      Version December 2015                   //
//							Incomplete							//
//////////////////////////////////////////////////////////////////
// Added short names for long names based in Julian coordinates //
//				Unconfirmed browns also commented               //
//////////////////////////////////////////////////////////////////

// Star solver log level:
// 0 - do not log
// 1 - log errors and warnings only
// 2 - log everything
LogLevel    1


//Gliese 22;6thCVB, spanish wiki
//very good system

Barycenter "Gliese 22 A"
{
	ParentBody "Gliese 22"
	Orbit
	{
		Period          222.3
		SemiMajorAxis 	14.82699438
		Eccentricity 	0.293
		Inclination 	47.3
		AscendingNode 	174.9
		ArgOfPericenter 146.3
		Epoch 			2400191.480249
		MeanAnomaly 	0
	}
}

Barycenter "Gliese 22 B"
{
	ParentBody "Gliese 22"
	Orbit
	{
		Period 			222.3
		SemiMajorAxis 	18.80061298
		Eccentricity 	0.293
		Inclination 	47.3
		AscendingNode 	174.9
		ArgOfPericenter 326.3
		Epoch 			2400191.480249
		MeanAnomaly 	0
	}
}

Star "Gliese 22 Aa/GL 22 Aa/V547 Cas Aa/HIP 2552 Aa/2MASS J00322970+6714080 Aa"
{
	ParentBody "Gliese 22 A"		//total A spectra M2.5 V
	Radius 	   327120
	AppMagn    10.3
	MassSol    0.42					//unknown
	Orbit
	{
		Period 			15.64
		SemiMajorAxis 	0.8276319
		Eccentricity 	0.174
		Inclination 	44.6
		AscendingNode 	175.1
		ArgOfPericenter 106.8
		Epoch 			2451822.117469
		MeanAnomaly 	0
	}
}

Star "Gliese 22 Ab/GL 22 Ab/V547 Cas Ab/HIP 2552 Ab/2MASS J00322970+6714080 Ab"
{
	ParentBody "Gliese 22 A"
	MassSol    0.08					//close to brown dwarf mass
	Orbit
	{
		Period 			15.64
		SemiMajorAxis 	4.34506748
		Eccentricity 	0.174
		Inclination 	44.6
		AscendingNode 	175.1
		ArgOfPericenter 286.8
		Epoch 			2451822.117469
		MeanAnomaly 	0
	}
}

Star "Gliese 22 Ba/GL 22 Ba/V547 Cas Ba/HIP 2552 Ba/LFT 47 Ba/BD+66 24 Ba/2MASS J00322970+6714080 Ba"
{
	ParentBody "Gliese 22 B"
	Class 	   "M3.5 V"
	Radius     222720
	AppMagn    12.2
	MassSol    0.38
	Orbit
	{
		Period 			15
		SemiMajorAxis 	0.12794731
		Eccentricity 	0.083
		Inclination 	47
		AscendingNode 	175
		ArgOfPericenter 347
		Epoch 			2455196.955386
		MeanAnomaly 	0
	}
}

Star "Gliese 22 Bb/GL 22 Bb/V547 Cas Bb/HIP 2552 Bb/LFT 47 Bb/BD+66 24 Bb/2MASS J00322970+6714080 Bb"
{
	ParentBody "Gliese 22 B"
	Class 	   "L V"						//Unknown
	MassSol    0.01425375
	DiscDate   "2008"
	Orbit
	{
		Period        	15
		SemiMajorAxis 	3.39475207
		Eccentricity  	0.083
		Inclination   	47
		AscendingNode 	175
		ArgOfPericenter 167
		Epoch 			2455196.955386
		MeanAnomaly 	0
	}
}

Barycenter "GJ 1001 (AC)/Gliese 1001 (AC)"
{
	ParentBody "GJ 1001"
	Orbit
	{
		Period          4122.305	//Generic
		SemiMajorAxis   19.2997
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "GJ 1001 A/Gliese 1001 A/LHS 102/LTT 20/WDS J00046-4044 A"
{
	ParentBody "GJ 1001 (AC)"
	Class      "M4 V"
	AppMagn    12.84
	Orbit
	{
		Period          4
		SemiMajorAxis   0.1023
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "GJ 1001 C/Gliese 1001 C/2MASS J00043484-4044058 C"
{
	ParentBody "GJ 1001 (AC)"
	Class      "L5 V"
	MassSol    0.034209
	DiscDate   "2004"
	Orbit
	{
		Period          4
		SemiMajorAxis   0.7177
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "GJ 1001 B/Gliese 1001 B/2MASS J00043484-4044058 B"
{
	ParentBody "GJ 1001"
	Class      "L5 V"
	AppMagnI   16.68
	AppMagnJ   13.11
	AppMagnH   12.06
	AppMagnKs  11.4
	AppMagnK   11.4
	Teff       1600
	Radius     40548.38
	MassSol    0.034209
	DiscDate   "1999"
	Orbit
	{
		Period          4122.305	//Generic
		SemiMajorAxis   154.7003
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "SDSS 00064-08524 A"
{
	ParentBody "SDSS 00064-08524"
	Class      "M9 V"
	AppMagn    15
	Orbit
	{
		Period          0.404
		SemiMajorAxis   0.1336
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "SDSS 00064-08524 B/SDSS J000649.16-085246.3 B/2MASS J00064916-0852457 B"
{
	ParentBody "SDSS 00064-08524"
	Class      "T5 V"
	AppMagnr   20.98
	AppMagni   18.19
	AppMagnJ   14.14
	AppMagnH   13.55
	AppMagnK   13.13
	MassSol    0.057015
	DiscDate   "2012"
	Orbit
	{
		Period          0.404
		SemiMajorAxis   0.1524
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "Koenigstuhl 1 A/KO 1 A/LEHPM 494/WDS J00212-4246 A"
{
	ParentBody "Koenigstuhl 1"
	Class      "M9 V"
	Orbit
	{
		Period          170000
		SemiMajorAxis   992.1061
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "DENIS-P 00210-42444/DENIS-P J002105.8-424442/2MASS J00210589-4244433"           //Unconfirmed
{
	ParentBody "Koenigstuhl 1"
	Class      "M9.5 V"
	AppMagnI   16.79
	AppMagnJ   13.52
	AppMagnH   12.81
	AppMagnK   12.3
	MassSol    0.079821
	Orbit
	{
		Period          170000
		SemiMajorAxis   807.8939
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}
Barycenter "LHS 1070 (AC)"
{
	ParentBody "LHS 1070"
	Orbit
	{
		Period          44.4
		SemiMajorAxis   2.6706
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "LHS 1070 A"
{
	ParentBody "LHS 1070 (AC)"
	Class      "M6 V"
	AppMagn    15.46
	Orbit
	{
		Period          17.24
		SemiMajorAxis   1.4574
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "LHS 1070 C/2MASS J00244419-2708242"
{
	ParentBody "LHS 1070 (AC)"
	Class      "M9 V"
	MassSol    0.0703185
	Orbit
	{
		Period          17.24
		SemiMajorAxis   2.0726
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "LHS 1070 B/2MASS J00244419-2708242 B"
{
	ParentBody "LHS 1070"
	Class      "M8.5 V"
	AppMagn    15.42
	AppMagnR   13.58
	AppMagnI   11.4
	AppMagnJ   9.25
	AppMagnH   8.55
	AppMagnK   8.24
	MassSol    0.07697025
	Orbit
	{
		Period          44.4
		SemiMajorAxis   5.9094
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "LP 349-25 A/2MASS J00275592+2219328 A"
{
	ParentBody "LP 349-25"
	Class      "M8 V"
	AppMagn    17.56
	AppMagnR   16
	AppMagnI   12.4
	AppMagnJ   11.15
	AppMagnH   10.61
	AppMagnK   10.15
	Teff       2780
	Radius     88786.97
	MassSol    0.064617
	DiscDate   "1995"
	Orbit
	{
		Period          7.31
		SemiMajorAxis   0.8641
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "LP 349-25 B/2MASS J00275592+2219328 B"
{
	ParentBody "LP 349-25"
	Class      "M8 V"
	AppMagnJ   11.51
	AppMagnH   10.93
	AppMagnK   10.46
	Teff       2640
	Radius     84592.31
	MassSol    0.05606475
	DiscDate   "2010"
	Orbit
	{
		Period          7.31
		SemiMajorAxis   0.9959
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "2MASS 00413-56211 B/2MASS J00413538-5621127 B"
{
	ParentBody "2MASS 00413-56211"
	Class      "M9 V"
	AppMagnJ   13.22
	AppMagnH   12.53
	AppMagnKs  12.01
	MassSol    0.01425375
	Orbit
	{
		Period          126
		SemiMajorAxis   5.9333
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "2MASS 00413-56211 A/2MASS J00413538-5621127 A"
{
	ParentBody "2MASS 00413-56211"
	Class      "M8 V"
	AppMagnI   14.69
	AppMagnJ   12.37
	AppMagnH   11.75
	AppMagnKs  11.32
	AppMagnK   10.86
	Teff       2600
	MassSol    0.0285075
	DiscDate   "1995"
	Orbit
	{
		Period          126
		SemiMajorAxis   2.9667
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "HD 4747 A"
{
	ParentBody "HD 4747"
	Class      "G8 V"
	AppMagn    7.15
	Orbit
	{
		Period          31.7
		SemiMajorAxis   0.419
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "HD 4747 b/2MASS J00492678-2312447 b"           //Unconfirmed
{
	ParentBody "HD 4747"
	MassSol    0.0437115
	DiscDate   "2002"
	Orbit
	{
		Period          31.7
		SemiMajorAxis   9.011
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "HD 8765 A/HIP 6712"
{
	ParentBody "HD 8765 (AB)"
	Class      "G8 V"
	AbsMagn    3.78
	Orbit
	{
		Period          12
		SemiMajorAxis   0.225
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "HD 8765 b/2MASS J01261920-0440267 b"           //Unconfirmed
{
	ParentBody "HD 8765 (AB)"
	MassSol    0.04086075
	DiscDate   "2007"
	Orbit
	{
		Period          12
		SemiMajorAxis   5.175
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "DENIS-P 01303-44454 A/DENIS-P J013035.5-444541 A/2MASS J01303563-4445411 A"
{
	ParentBody "DENIS-P 01303-44454"
	Class      "M9 V"
	AppMagnI   17.18
	AppMagnJ   14.12
	AppMagnH   13.48
	AppMagnKs  12.99
	AppMagnK   12.99
	Teff       2400
	MassSol    0.0817215
	DiscDate   "2011"
	Orbit
	{
		Period          3860
		SemiMajorAxis   57.871
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "DENIS-P 01303-44454 B/DENIS-P J013035.5-444541 B/2MASS J01303563-4445411 B"
{
	ParentBody "DENIS-P 01303-44454"
	Class      "L7.5 V"
	AppMagnJ   17.28
	AppMagnH   16.13
	AppMagnKs  15.34
	AppMagnK   15.34
	Teff       1450
	MassSol    0.06556725
	DiscDate   "2011"
	Orbit
	{
		Period          3860
		SemiMajorAxis   72.129
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}
Barycenter "DENIS-P 02052-11592 (AC)"
{
	ParentBody "DENIS-P 02052-11592"
	Orbit
	{
		Period          105
		SemiMajorAxis   3.2767
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "DENIS-P 02052-11592 A/DENIS-P J020529.0-115925 A/2MASS J02052940-1159296 A"
{
	ParentBody "DENIS-P 02052-11592 (AC)"
	Class      "L7 V"
	AppMagnI   18.3
	AppMagnJ   14.59
	AppMagnH   13.57
	AppMagnKs  13
	AppMagnW1  12.22
	AppMagnW2  11.72
	AppMagnW3  10.87
	MassSol    0.049413
	DiscDate   "1997"
	Orbit
	{
		Period          4.9467    //Generic
		SemiMajorAxis   0.5809
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "DENIS-P 02052-11592 C/DENIS-P J020529.0-115925 C/2MASS J02052940-1159296 C"           //Unconfirmed
{
	ParentBody "DENIS-P 02052-11592 (AC)"
	Class      "T0 V"
	MassSol    0.0399105
	DiscDate   "2005"
	Orbit
	{
		Period          4.9467    //Generic
		SemiMajorAxis   0.7191
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "DENIS-P 02052-11592 B/DENIS-P J020529.0-115925 B/2MASS J02052940-1159296 B"
{
	ParentBody "DENIS-P 02052-11592"
	Class      "L7 V"
	MassSol    0.049413
	DiscDate   "1997"
	Orbit
	{
		Period          105
		SemiMajorAxis   5.9233
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "HD 14651 A/HIP 11028 A"
{
	ParentBody "HD 14651"
	Class      "G0 V"
	AbsMagn    5.29
	Orbit
	{
		Period          0.2174
		SemiMajorAxis   0.0141
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "HD 14651 b/2MASS J02220085+0444483 b"           //Unconfirmed
{
	ParentBody "HD 14651"
	MassSol    0.04466175
	Orbit
	{
		Period          0.2174
		SemiMajorAxis   0.3469
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "GJ 1048 A/Gliese 1048 A/HIP 12110 A/HD 16270 A"
{
	ParentBody "GJ 1048"
	Class      "K3 V"
	AbsMagn    6.7
	Orbit
	{
		Period          4222.757    //Generic
		SemiMajorAxis   17.713
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "GJ 1048 B/2MASS J02355993-2331205 B"
{
	ParentBody "GJ 1048"
	Class      "L1 V"
	AppMagnJ   13.67
	AppMagnH   12.73
	AppMagnKs  12.19
	Teff       2050
	Radius     82494.98
	MassSol    0.06176625
	DiscDate   "2001"
	Orbit
	{
		Period          4222.757    //Generic
		SemiMajorAxis   232.287
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "HD 19467 A/HIP 14501 A"
{
	ParentBody "HD 19467"
	Class      "G3 V"
	AbsMagn    4.5
	Orbit
	{
		Period          1110
		SemiMajorAxis   3.7251
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "HD 19467 B/2MASS J03071857-1345421 B"
{
	ParentBody "HD 19467"
	Class      "T6 V"
	AppMagnJ   17.61
	AppMagnH   17.9
	AppMagnKs  17.97
	Teff       1050
	MassSol    0.05416425
	DiscDate   "2014"
	Orbit
	{
		Period          1110
		SemiMajorAxis   68.7749
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "AE For A/CD-25 1273/NLTT 9990/Gliese 3203 A/GJ 3203 A/L 587-3/HIP 14568 A"
{
	ParentBody "AE For"
	Class      "K7 V"
	AppMagn    10.87
	Orbit
	{
		Period          8.532    //Generic
		SemiMajorAxis   0.2793
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "AE For C/2MASS J03080663-2445347"           //Unconfirmed
{
	ParentBody "AE For"
	MassSol    0.05226375
	DiscDate   "2012"
	Orbit
	{
		Period          8.532    //Generic
		SemiMajorAxis   3.4207
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "2MASSW 03105+16481 A/2MASSW J0310599+164816 A/2MASS J03105986+1648155 A"
{
	ParentBody "2MASSW 03105+16481"
	Class      "L9 V"
	AppMagn    24.8
	AppMagnJ   16.73
	AppMagnH   15.66
	AppMagnKs  15.07
	MassSol    0.0209055
	DiscDate   "2000"
	Orbit
	{
		Period          72
		SemiMajorAxis   2.6
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "2MASSW 03105+16481 B/2MASSW J0310599+164816 B/2MASS J03105986+1648155 B"
{
	ParentBody "2MASSW 03105+16481"
	Class      "L9 V"
	AppMagnJ   16.83
	AppMagnH   15.71
	AppMagnKs  15.06
	MassSol    0.0209055
	DiscDate   "2010"
	Orbit
	{
		Period          72
		SemiMajorAxis   2.6
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "SBC9 3013 A/2MASS J03202839-0446358 A/2MASSW J0320284-044536"
{
	ParentBody "SBC9 3013"
	Class      "M8.5 V"
	Orbit
	{
		Period          0.676
		SemiMajorAxis   3.7465
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "2MASS 03202-04463 B/2MASS J03202839-0446358 B"
{
	ParentBody "SBC9 3013"
	Class      "T5 V"
	AppMagnJ   13.26
	AppMagnH   12.54
	AppMagnKs  12.13
	Teff       1900
	MassSol    0.06746775
	DiscDate   "2008"
	Orbit
	{
		Period          0.676
		SemiMajorAxis   4.5535
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "2MASSW 03370-17580 A/2MASSW J0337036-175807 A/2MASS J03370359-1758079 A"
{
	ParentBody "2MASSW 03370-17580"
	Class      "L4.5 V"
	AppMagn    23.6
	AppMagnI   19.4
	AppMagnJ   15.62
	AppMagnH   14.41
	AppMagnKs  13.58
	AppMagnW1  12.83
	AppMagnW2  12.45
	AppMagnW3  11.87
	Teff       1910
	MassSol    0.05796525
	DiscDate   "2000"
	Orbit
	{
		Period          14.9464    //Generic
		SemiMajorAxis   0.4704
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "2MASSW 03370-17580 B/2MASSW J0337036-175807 B/2MASS J03370359-1758079 B"
{
	ParentBody "2MASSW 03370-17580"
	Class      "T8 V"
	Teff       615
	MassSol    0.0133035
	DiscDate   "2010"
	Orbit
	{
		Period          14.9464    //Generic
		SemiMajorAxis   2.0496
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "2MASS 03423+12162 A"
{
	ParentBody "2MASS 03423+12162"
	Class      "M5.2 V"
	Orbit
	{
		Period          166
		SemiMajorAxis   7
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "2MASS 03423+12162 B/2MASS J03423180+1216225 B"           //Unconfirmed
{
	ParentBody "2MASS 03423+12162"
	Class      "L0 V"
	DiscDate   "2012"
	Orbit
	{
		Period          166
		SemiMajorAxis   7
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "2MASS 03453+24524 B/2MASS J03453136+2452476 B"
{
	ParentBody "2MASS 03453+24524"
	Class      "L4 V"
	MassSol    0.03801
	Orbit
	{
		Period          106
		SemiMajorAxis   4.2138
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "2MASS 03453+24524 A/2MASS J03453136+2452476 A"
{
	ParentBody "2MASS 03453+24524"
	Class      "L1 V"
	AppMagnI   18.35
	AppMagnJ   15.43
	AppMagnH   14.73
	AppMagnKs  14.36
	MassSol    0.04466175
	Orbit
	{
		Period          106
		SemiMajorAxis   3.5862
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "IPMBD 25 A/2MASS J03462608+2405096 A"
{
	ParentBody "IPMBD 25"
	Class      "M7 V"
	AppMagnI   17.82
	AppMagnJ   15.19
	AppMagnH   14.61
	AppMagnKs  14.16
	MassSol    0.0627165
	Orbit
	{
		Period          197
		SemiMajorAxis   4.828
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "IPMBD 25 B/2MASS J03462608+2405096 B"
{
	ParentBody "IPMBD 25"
	Class      "L4 V"
	MassSol    0.03896025
	Orbit
	{
		Period          197
		SemiMajorAxis   7.772
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "HD 23514 A/SAO 76178/AG+22 346/CL Melotte 22 1132"
{
	ParentBody "HD 23514"
	Class      "F5 V"
	AppMagn    9.43
	Orbit
	{
		Period          5799.9125    //Generic
		SemiMajorAxis   15.6187
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "HD 23514 B/2MASS J03463839+2255112 B"
{
	ParentBody "HD 23514"
	Class      "M7 V"
	AppMagnKs  14.92
	Teff       2600
	MassSol    0.05986575
	DiscDate   "2012"
	Orbit
	{
		Period          5799.9125    //Generic
		SemiMajorAxis   344.3813
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "PPl 15 A"
{
	ParentBody "PPl 15"
	Class      "M7 V"
	AppMagn    22.46
	AppMagnI   17.91
	AppMagnJ   15.28
	AppMagnH   14.7
	AppMagnKs  14.26
	MassSol    0.06936825
	Orbit
	{
		Period          0.0159
		SemiMajorAxis   0.0139
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "PPl 15 B"
{
	ParentBody "PPl 15"
	Class      "M8 V"
	MassSol    0.05986575
	Orbit
	{
		Period          0.0159
		SemiMajorAxis   0.0161
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "CFHT-Pl-12 A/2MASS J03535511+2323363 A"
{
	ParentBody "CFHT-Pl-12"
	Class      "M8 V"
	AppMagnR   20.55
	AppMagnI   17.75
	AppMagnJ   15.22
	AppMagnH   14.55
	AppMagnKs  14.05
	MassSol    0.05416425
	Orbit
	{
		Period          111
		SemiMajorAxis   3.4227
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "CFHT-Pl-12 B/2MASS J03535511+2323363 B"
{
	ParentBody "CFHT-Pl-12"
	Class      "L4 V"
	MassSol    0.03801
	Orbit
	{
		Period          111
		SemiMajorAxis   4.8773
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "2MASS 04080+28072 A"
{
	ParentBody "2MASS 04080+28072"
	Class      "M3.5 V"
	Orbit
	{
		Period          24.3486    //Generic
		SemiMajorAxis   0.5806
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "2MASS 04080+28072 B/2MASS J04080782+2807280 B"           //Unconfirmed
{
	ParentBody "2MASS 04080+28072"
	AppMagnJ   10.11
	AppMagnH   9.55
	AppMagnK   9.34
	MassSol    0.0399105
	DiscDate   "2007"
	Orbit
	{
		Period          24.3486    //Generic
		SemiMajorAxis   5.8194
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "V1312 Tau A/HBC 371/HBC 371/WDS J04176+2833 A/JH 153"
{
	ParentBody "V1312 Tau"
	Class      "M2 V"
	AppMagn    13.26
	Orbit
	{
		Period          24.3899    //Generic
		SemiMajorAxis   0.6206
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "V1312 Tau B/2MASS J04173893+2833005 B"           //Unconfirmed
{
	ParentBody "V1312 Tau"
	MassSol    0.049413
	DiscDate   "2011"
	Orbit
	{
		Period          24.3899    //Generic
		SemiMajorAxis   6.2794
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "LP 415-20 A/WDS J04218+1929 A"
{
	ParentBody "LP 415-20"
	Class      "M7 V"
	AppMagn    19.23
	Orbit
	{
		Period          14.4
		SemiMajorAxis   1.0122
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "LP 415-20 B"           //Unconfirmed
{
	ParentBody "LP 415-20"
	Class      "M9.5 V"
	AppMagnJ   13.93
	AppMagnH   13.1
	AppMagnK   12.63
	Teff       2000
	Radius     69911
	MassSol    0.07887075
	DiscDate   "2003"
	Orbit
	{
		Period          14.4
		SemiMajorAxis   1.2578
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "SDSS 04234-04140 A/SDSS J042348.57-041403.5 A/2MASS J04234858-0414035 A"
{
	ParentBody "SDSS 04234-04140"
	Class      "L6.5 V"
	AppMagnr   22.64
	AppMagni   20.14
	AppMagnJ   15.02
	AppMagnH   13.91
	AppMagnKs  13.26
	AppMagnW1  12.18
	AppMagnW2  11.56
	AppMagnW3  10.54
	MassSol    0.05986575
	DiscDate   "2002"
	Orbit
	{
		Period          19
		SemiMajorAxis   1.1304
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "SDSS 04234-04140 B/SDSS J042348.57-041403.5 B/2MASS J04234858-0414035 B"
{
	ParentBody "SDSS 04234-04140"
	Class      "T2 V"
	AppMagnJ   15.46
	AppMagnH   14.64
	AppMagnKs  14.39
	MassSol    0.049413
	Orbit
	{
		Period          19
		SemiMajorAxis   1.3696
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "LP 475-855 A"
{
	ParentBody "LP 475-855"
	Class      "M7 V"
	AppMagn    20.8
	Orbit
	{
		Period          85
		SemiMajorAxis   3.8155
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "LP 475-855 B/2MASS J04290287+1337586 B"           //Unconfirmed
{
	ParentBody "LP 475-855"
	Class      "M9.5 V"
	AppMagn    20.8
	AppMagnI   14.81
	AppMagnJ   12.65
	AppMagnH   11.94
	AppMagnK   11.62
	MassSol    0.079821
	Orbit
	{
		Period          85
		SemiMajorAxis   4.6845
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "2MASS 04291-31235 A"
{
	ParentBody "2MASS 04291-31235"
	Class      "M7.5 V"
	Orbit
	{
		Period          48
		SemiMajorAxis   2.5864
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "2MASS 04291-31235 B/2MASS J04291842-3123568 B"           //Unconfirmed
{
	ParentBody "2MASS 04291-31235"
	Class      "L1 V"
	AppMagnJ   12.38
	AppMagnH   11.65
	AppMagnKs  11.12
	MassSol    0.07887075
	DiscDate   "2003"
	Orbit
	{
		Period          48
		SemiMajorAxis   3.2136
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "CFHT-Tau 7 A/2MASS J04321786+2422149 A"
{
	ParentBody "CFHT-Tau 7"
	Class      "M6 V"
	AppMagn    18.2
	AppMagnR   16.63
	AppMagnI   14.12
	AppMagnJ   11.54
	AppMagnH   10.79
	AppMagnKs  10.4
	AppMagnK   10.38
	Teff       2830
	MassSol    0.06936825
	Orbit
	{
		Period          710
		SemiMajorAxis   14.8235
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "CFHT-Tau 7 B/2MASS J04321786+2422149 B"
{
	ParentBody "CFHT-Tau 7"
	Class      "M7 V"
	MassSol    0.05986575
	Orbit
	{
		Period          710
		SemiMajorAxis   17.1765
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "JH 223 A/XEST 07-011/2MASS J04404950+2551191 A"
{
	ParentBody "JH 223"
	Class      "M2 V"
	AppMagn    15.5
	Orbit
	{
		Period          6992.2753    //Generic
		SemiMajorAxis   26.9813
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "2MASS 04404+25511 B/2MASS J04404950+2551191 B"           //Unconfirmed
{
	ParentBody "JH 223"
	AppMagnJ   13.36
	AppMagnH   12.59
	AppMagnK   12.19
	MassSol    0.049413
	DiscDate   "2007"
	Orbit
	{
		Period          6992.2753    //Generic
		SemiMajorAxis   273.0187
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "2MASS 04414+23015 A/2MASS J04414565+2301580 A"           //Unconfirmed
{
	ParentBody "2MASS 04414+23015"
	AppMagnJ   10.74
	AppMagnH   10.1
	AppMagnKs  9.85
	MassSol    0.0399105
	DiscDate   "2011"
	Orbit
	{
		Period          292885.3789    //Generic
		SemiMajorAxis   716
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "2MASS 04414+23015 B/2MASS J04414565+2301580 B"
{
	ParentBody "2MASS 04414+23015"
	AppMagnH   13.03
	AppMagnKs  12.59
	MassSol    0.026607
	DiscDate   "2007"
	Orbit
	{
		Period          292885.3789    //Generic
		SemiMajorAxis   1074
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "2MASS 05185-28283 A/2MASS J05185995-2828372 A"
{
	ParentBody "2MASS 05185-28283"
	Class      "L6 V"
	AppMagnJ   16.67
	AppMagnH   15.11
	AppMagnKs  14.3
	AppMagnW1  13.41
	AppMagnW2  12.82
	AppMagnW3  11.73
	MassSol    0.06936825
	DiscDate   "2002"
	Orbit
	{
		Period          10
		SemiMajorAxis   0.7488
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "2MASS 05185-28283 B/2MASS J05185995-2828372 B"
{
	ParentBody "2MASS 05185-28283"
	Class      "T4 V"
	AppMagnJ   16.8
	AppMagnH   16.44
	AppMagnKs  16.48
	MassSol    0.049413
	Orbit
	{
		Period          10
		SemiMajorAxis   1.0512
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "2MASS 05254-74252 A"
{
	ParentBody "2MASS 05254-74252"
	Class      "M5 V"	//Unknown,supposed low mass star
	Orbit
	{
		Period          191192.4703    //Generic
		SemiMajorAxis   634.0176
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "2MASS 05254-74252 B/2MASS J05254550-7425263 B/2MASS J05253876-7426008 B"
{
	ParentBody "2MASS 05254-74252"
	Class      "L2 V"
	AppMagnJ   15.71
	AppMagnH   14.97
	AppMagnK   14.43
	AppMagnW1  14.02
	AppMagnW2  13.57
	AppMagnW3  11.97
	Teff       2100
	MassSol    0.06936825
	Orbit
	{
		Period          191192.4703    //Generic
		SemiMajorAxis   1370.9824
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "2MASS 05352-05460 A/2MASS J05352184-0546085 A"
{
	ParentBody "2MASS 05352-05460"
	Class      "M6.5 V"
	AppMagnR   19.21
	AppMagnI   17.28
	AppMagnJ   14.65
	AppMagnH   13.9
	AppMagnK   13.47
	Teff       2715
	Radius     470501.03
	MassSol    0.057015
	Orbit
	{
		Period          0.02678
		SemiMajorAxis   0.0158
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "2MASS 05352-05460 B/2MASS J05352184-0546085 B"
{
	ParentBody "2MASS 05352-05460"
	Class      "M6.5 V"
	Teff       2850
	Radius     368430.97
	MassSol    0.0361095
	Orbit
	{
		Period          0.02678
		SemiMajorAxis   0.0249
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "2MASS 05464-07574 A"
{
	ParentBody "2MASS 05464-07574"
	Class      "M3 V"
	Orbit
	{
		Period          3817
		SemiMajorAxis   64.15
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "2MASS 05464-07574 B/2MASS J05464932-0757427 B"
{
	ParentBody "2MASS 05464-07574"
	Class      "L0 V"
	DiscDate   "2012"
	Orbit
	{
		Period          3817
		SemiMajorAxis   64.15
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "DENIS-J055146.0-443412.2 A/2MASS J05514604-4434128 A"
{
	ParentBody "DENIS-J055146.0-443412.2"
	Class      "M8.5 V"
	AppMagnJ   15.79
	AppMagnH   15.2
	AppMagnKs  14.87
	MassSol    0.06936825
	Orbit
	{
		Period          11500
		SemiMajorAxis   101.9118
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "DENIS-J055146.0-443412.2 B/2MASS J05514604-4434128 B"
{
	ParentBody "DENIS-J055146.0-443412.2"
	Class      "L0 V"
	MassSol    0.05986575
	Orbit
	{
		Period          11500
		SemiMajorAxis   118.0882
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "DENIS 06300-18401 A/DENIS J063001.4-184014 A/2MASS J06300140-1840143 A"           //Unconfirmed
{
	ParentBody "DENIS 06300-18401"
	Class      "M8.5 V"
	AppMagnI   15.88
	AppMagnJ   12.68
	AppMagnH   11.93
	AppMagnKs  11.46
	AppMagnK   11.46
	MassSol    0.0855225
	DiscDate   "2008"
	Orbit
	{
		Period          3.0665
		SemiMajorAxis   0.4851
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "DENIS 06300-18401 B/DENIS J063001.4-184014 B/2MASS J06300140-1840143 B"
{
	ParentBody "DENIS 06300-18401"
	MassSol    0.06746775
	DiscDate   "2015"
	Orbit
	{
		Period          3.0665
		SemiMajorAxis   0.6149
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "TYC 2949-00557-1 A/GSC 92949-00557"
{
	ParentBody "TYC 2949-00557-1"
	Class      "F V"	//Unknown subclass
	Orbit
	{
		Period          0.01559
		SemiMajorAxis   0.0037
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "TYC 2949-00557 B/TYC 2949-00557-1 B"           //Unconfirmed
{
	ParentBody "TYC 2949-00557-1"
	MassSol    0.07126875
	DiscDate   "2010"
	Orbit
	{
		Period          0.01559
		SemiMajorAxis   0.0683
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "HD 49197 A"
{
	ParentBody "HD 49197"
	Class      "F5 V"
	AppMagn    7.3
	Orbit
	{
		Period          231.8418    //Generic
		SemiMajorAxis   1.5716
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "HD 49197 B"
{
	ParentBody "HD 49197"
	Class      "L4 V"
	AppMagnJ   15.92
	AppMagnH   14.62
	AppMagnKs  14.29
	MassSol    0.0513135
	DiscDate   "2004"
	Orbit
	{
		Period          231.8418    //Generic
		SemiMajorAxis   40.4284
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "2MASS 06523+47103 A/2MASS J06523073+4710348 A"           //Unconfirmed
{
	ParentBody "2MASS 06523+47103"
	Class      "L3.5 V"
	AppMagn    21.4
	AppMagnJ   13.51
	AppMagnH   12.38
	AppMagnKs  11.69
	AppMagnW1  10.87
	AppMagnW2  10.51
	AppMagnW3  9.86
	Teff       1600
	Radius     41247.49
	MassSol    0.07506975
	DiscDate   "2003"
	Orbit
	{
		Period          10
		SemiMajorAxis   0.9673
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "2MASS 06523+47103 B/2MASS J06523073+4710348 B"
{
	ParentBody "2MASS 06523+47103"
	Class      "L6.5 V"
	MassSol    0.0703185
	Orbit
	{
		Period          10
		SemiMajorAxis   1.0327
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "DENIS-P 06524-57413 A/DENIS-P J065248.5-574137 A/2MASS J06524851-5741376 A"
{
	ParentBody "DENIS-P 06524-57413"
	Class      "M8 V"
	AppMagnI   16.57
	AppMagnJ   14.22
	AppMagnH   13.57
	AppMagnK   13.05
	MassSol    0.03801
	DiscDate   "2008"
	Orbit
	{
		Period          110
		SemiMajorAxis   3.65
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "DENIS-P 06524-57413 B/DENIS-P J065248.5-574137 B/2MASS J06524851-5741376 B"
{
	ParentBody "DENIS-P 06524-57413"
	AppMagnJ   14.57
	AppMagnH   13.9
	AppMagnK   13.37
	MassSol    0.03801
	DiscDate   "2012"
	Orbit
	{
		Period          110
		SemiMajorAxis   3.65
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "HD 52756 A"
{
	ParentBody "HD 52756"
	Class      "K1 V"
	AppMagn    8.47
	Orbit
	{
		Period          0.1447
		SemiMajorAxis   0.0162
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "HD 52756 b/2MASS J07002959-4122475 b"           //Unconfirmed
{
	ParentBody "HD 52756"
	MassSol    0.05606475
	DiscDate   "2010"
	Orbit
	{
		Period          0.1447
		SemiMajorAxis   0.2488
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "2MASS 07003+31572 A/2MASS J07003664+3157266 A"
{
	ParentBody "2MASS 07003+31572"
	Class      "L3.5 V"
	AppMagnJ   13.17
	AppMagnH   12.21
	AppMagnKs  11.58
	Teff       2100
	Radius     58725.24
	MassSol    0.0703185
	DiscDate   "1999"
	Orbit
	{
		Period          12
		SemiMajorAxis   0.9657
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "2MASS 07003+31572 B/2MASS J07003664+3157266 B"
{
	ParentBody "2MASS 07003+31572"
	Class      "L6 V"
	AppMagnJ   14.66
	AppMagnH   13.61
	AppMagnKs  12.97
	MassSol    0.05986575
	Orbit
	{
		Period          12
		SemiMajorAxis   1.1343
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "2MASS 07464+20003 A/2MASS J07464256+2000321 A"
{
	ParentBody "2MASS 07464+20003"
	Class      "L0.5 V"
	AppMagn    19.87
	AppMagnR   17.4
	AppMagni   16.09
	AppMagnI   15.11
	AppMagnJ   12.28
	AppMagnH   11.56
	AppMagnKs  10.47
	AppMagnK   11.05
	AppMagnW1  10.13
	AppMagnW2  9.89
	AppMagnW3  9.38
	Teff       2205
	Radius     69211.89
	MassSol    0.0703185
	DiscDate   "2000"
	Orbit
	{
		Period          12.71
		SemiMajorAxis   1.3322
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "2MASS 07464+20003 B/2MASS J07464256+2000321 B"
{
	ParentBody "2MASS 07464+20003"
	Class      "L1.5 V"
	AppMagnJ   12.79
	AppMagnH   12
	AppMagnK   11.41
	Teff       2060
	Radius     67813.67
	MassSol    0.05986575
	DiscDate   "2000"
	Orbit
	{
		Period          12.71
		SemiMajorAxis   1.5648
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "HD 65486 A/HIP 38939 A"
{
	ParentBody "HD 65486"
	Class      "K3 V"
	AppMagn    8.42
	Orbit
	{
		Period          71359.8988    //Generic
		SemiMajorAxis   69.5637
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "HD 65486 B/2MASS J07580434-2537356 B"
{
	ParentBody "HD 65486"
	Class      "T4.5 V"
	AppMagnJ   16.12
	AppMagnH   15.8
	AppMagnKs  15.86
	AppMagnW1  15.92
	AppMagnW2  13.82
	Teff       1090
	MassSol    0.0361095
	DiscDate   "2012"
	Orbit
	{
		Period          71359.8988    //Generic
		SemiMajorAxis   1560.4363
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "SDSS-J080531.84+481233.0 A/2MASS J08053189+4812330 A"
{
	ParentBody "SDSS-J080531.84+481233.0"
	Class      "L4.5 V"
	AppMagnr   22.39
	AppMagni   19.59
	AppMagnJ   14.73
	AppMagnH   13.92
	AppMagnKs  13.44
	AppMagnK   13.44
	AppMagnW1  12.89
	AppMagnW2  12.43
	AppMagnW3  11.97
	MassSol    0.0779205
	DiscDate   "1999"
	Orbit
	{
		Period          2.6074    //Generic
		SemiMajorAxis   0.4675	  //Unknown, generic 1 AU
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "SDSS-J080531.84+481233.0 B/2MASS J08053189+4812330 B"
{
	ParentBody "SDSS-J080531.84+481233.0"
	Class      "T5 V"
	MassSol    0.068418
	Orbit
	{
		Period          2.6074    //Generic
		SemiMajorAxis   0.5325	  //Unknown, generic 1 AU
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "DENIS-P 08230-49120 A/DENIS-P J0823031-491201 A/2MASS J08230313-4912012 A"
{
	ParentBody "DENIS-P 08230-49120"
	Class      "L1.5 V"
	AppMagnR   20.02
	AppMagnI   17.1
	AppMagnJ   13.55
	AppMagnH   12.64
	AppMagnKs  12.06
	MassSol    0.0741195
	DiscDate   "1997"
	Orbit
	{
		Period          0.675
		SemiMajorAxis   0.0976
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "DENIS-P 08230-49120 B/DENIS-P J0823031-491201 B/2MASS J08230313-4912012 B"
{
	ParentBody "DENIS-P 08230-49120"
	MassSol    0.02755725
	DiscDate   "2013"
	Orbit
	{
		Period          0.675
		SemiMajorAxis   0.2624
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "HD 72780 A/HIP 42112 A"
{
	ParentBody "HD 72780"
	Class      "F8 V"
	AppMagn    7.47
	Orbit
	{
		Period          55.4
		SemiMajorAxis   0.3939
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "HD 72780 b/2MASS J08350420+1117012 b"           //Unconfirmed
{
	ParentBody "HD 72780"
	MassSol    0.049413
	DiscDate   "2007"
	Orbit
	{
		Period          55.4
		SemiMajorAxis   9.4061
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}
Barycenter "WISEP 08381+15111 (AC)"
{
	ParentBody "WISEP 08381+15111"
	Mass       0.07316925
	Orbit
	{
		Period          421.4866    //Generic
		SemiMajorAxis   9.0776
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "WISEP 08381+15111 A/WISEP J083811.45+151115.1 A/2MASS J08381155+1511155 A"
{
	ParentBody "WISEP 08381+15111 (AC)"
	Class      "T3 V"
	AppMagnJ   17.57
	AppMagnH   17.1
	AppMagnKs  17.11
	AppMagnW1  15.71
	AppMagnW2  14.57
	AppMagnW3  12.29
	Teff       900
	MassSol    0.0399105
	DiscDate   "2011"
	Orbit
	{
		Period          14.5758    //Generic
		SemiMajorAxis   1.1364
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "WISEP 08381+15111 C/WISEP J083811.45+151115.1 C/2MASS J08381155+1511155 C"
{
	ParentBody "WISEP 08381+15111 (AC)"
	Class      "T4.5 V"
	AppMagnJ   17.98
	AppMagnH   17.78
	AppMagnKs  17.67
	MassSol    0.03325875
	DiscDate   "2013"
	Orbit
	{
		Period          14.5758    //Generic
		SemiMajorAxis   1.3636
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "WISEP 08381+15111 B/WISEP J083811.45+151115.1 B/2MASS J08381155+1511155 B"
{
	ParentBody "WISEP 08381+15111"
	Class      "T3 V"
	AppMagnJ   18.04
	AppMagnH   17.43
	AppMagnKs  17.46
	MassSol    0.03705975
	DiscDate   "2013"
	Orbit
	{
		Period          421.4866    //Generic
		SemiMajorAxis   17.9224
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "HD 74014 A/HIP 42634"
{
	ParentBody "HD 74014"
	Class      "K0 V"
	AbsMagn    4.94
	Orbit
	{
		Period          45
		SemiMajorAxis   0.5469
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "HD 74014 b/2MASS J08411855-0448320 b"           //Unconfirmed
{
	ParentBody "HD 74014"
	MassSol    0.04656225
	DiscDate   "2007"
	Orbit
	{
		Period          45
		SemiMajorAxis   10.4531
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "2MASS 08503+10571 A/2MASS J08503593+1057156 A"
{
	ParentBody "2MASS 08503+10571"
	Class      "L6 V"
	AppMagn    22.50
	AppMagnR   22.86
	AppMagnI   20.43
	AppMagnJ   16.88
	AppMagnH   15.65
	AppMagnKs  14.47
	AppMagnK   14.86
	Teff       1590
	Radius     69911
	MassSol    0.0399105
	DiscDate   "1999"
	Orbit
	{
		Period          24
		SemiMajorAxis   3269.863
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "2MASS 08503+10571 B/2MASS J08503593+1057156 B"
{
	ParentBody "2MASS 08503+10571"
	Class      "L8 V"
	AppMagnJ   17.7
	AppMagnH   16.45
	AppMagnK   15.77
	Teff       1380
	Radius     69911
	MassSol    0.02945775
	DiscDate   "1999"
	Orbit
	{
		Period          24
		SemiMajorAxis   4430.137
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "2MASS 08564+22351 A/2MASS J08564793+2235182 A"
{
	ParentBody "2MASS 08564+22351"
	Class      "L5 V"
	AppMagnI   19.2
	AppMagnJ   15.68
	AppMagnH   14.58
	AppMagnKs  13.95
	MassSol    0.0703185
	DiscDate   "2003"
	Orbit
	{
		Period          24
		SemiMajorAxis   1.6156
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "2MASS 08564+22351 B/2MASS J08564793+2235182 B"
{
	ParentBody "2MASS 08564+22351"
	Class      "L8 V"
	MassSol    0.06366675
	DiscDate   "2003"
	Orbit
	{
		Period          24
		SemiMajorAxis   1.7844
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "2MASS 09053-49183 A"
{
	ParentBody "2MASS 09053-49183"
	Class      "M4 V"
	Orbit
	{
		Period          5133
		SemiMajorAxis   69.05
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "2MASS 09053-49183 B/2MASS J09053033-4918382 B"           //Unconfirmed
{
	ParentBody "2MASS 09053-49183"
	Class      "L0 V"
	DiscDate   "2012"
	Orbit
	{
		Period          5133
		SemiMajorAxis   69.05
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}
//81 Cnc

Barycenter "81 Cnc (AB)"
{
	ParentBody "81 Cnc"
	Orbit
	{
		Period          19271.7089
		SemiMajorAxis   18.19837759
		Eccentricity    0
		Inclination     122.4
		AscendingNode   318.3
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star   "81 Cnc A/Gliese 337 A/GJ 337 A/GL 337 A/Gliese 337 A/PI1 Cnc A/HD 79096 A/HIP 45170 A/HR 3640 A"
{
	ParentBody "81 Cnc (AB)"
	Class      "G8 V"
	AppMagn     7.28
	MassSol     0.94
	Orbit
	{
		Period 			2.7043
		SemiMajorAxis 	1.144832
		Eccentricity 	0.417
		Inclination 	122.4
		AscendingNode 	318.3
		ArgOfPericenter 351.5
		Epoch 			2444236.183097
		MeanAnomaly 	0
	}
}

Star   "81 Cnc B/Gliese 337 B/GJ 337 B/GL 337 B/Gliese 337 B/PI1 Cnc B"
{
	ParentBody "81 Cnc (AB)"
	Class      "K1 V"
	AppMagn    7.47
	MassSol    0.86
	Orbit
	{
		Period 			2.7043
		SemiMajorAxis 	1.251328
		Eccentricity 	0.417
		Inclination 	122.4
		AscendingNode 	318.3
		ArgOfPericenter 171.5
		Epoch 			2444236.183097
		MeanAnomaly 	0
	}
}

Barycenter "81 Cnc (CD)"
{
	ParentBody "81 Cnc"
	Orbit
	{
		Period          19271.7089
		SemiMajorAxis   861.80162241
		Eccentricity    0
		Inclination     122.4
		AscendingNode   318.3
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star   "81 Cnc C/Gliese 337 C/GJ 337 C/GL 337 C/Gliese 337 C/PI1 Cnc C"
{
	ParentBody "81 Cnc (CD)"
	Class      "L8 V"
	AppMagnJ   15.51
	AppMagnH   14.62
	AppMagnKs  14.76
	AppMagnK   14.04
	MassSol    0.019005
	DiscDate   "2001"
	Orbit
	{
		Period          152
		SemiMajorAxis   5.45
		Inclination     122.4
		AscendingNode   318.3
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star   "81 Cnc D/Gliese 337 D/GJ 337 D/GL 337 D/Gliese 337 D/PI1 Cnc D"
{
	ParentBody "81 Cnc (CD)"
	Class      "T9	V"
	AppMagnKs  14.84
	MassSol    0.019005
	DiscDate   "2005"
	Orbit
	{
		Period          152
		SemiMajorAxis   5.45
		Inclination     122.4
		AscendingNode   318.3
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "2MASS 09153+04220 A/2MASS J09153413+0422045 A"
{
	ParentBody "2MASS 09153+04220"
	Class      "L7 V"
	AppMagnJ   14.55
	AppMagnH   13.53
	AppMagnKs  13.01
	AppMagnW1  12.08
	AppMagnW2  11.7
	AppMagnW3  10.95
	MassSol    0.07126875
	DiscDate   "2007"
	Orbit
	{
		Period          132
		SemiMajorAxis   5.4
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "2MASS 09153+04220 B/2MASS J09153413+0422045 B"
{
	ParentBody "2MASS 09153+04220"
	Class      "L7 V"
	MassSol    0.07126875
	Orbit
	{
		Period          132
		SemiMajorAxis   5.4
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "2MASS 09201+35174 A/2MASS J09201223+3517429 A"
{
	ParentBody "2MASS 09201+35174"
	Class      "L5.5 V"
	AppMagnI   19.4
	AppMagnJ   16.36
	AppMagnH   15.33
	AppMagnKs  13.98
	AppMagnK   14.58
	AppMagnW1  13.28
	AppMagnW2  12.79
	AppMagnW3  12.46
	Teff       1375
	Radius     69911
	MassSol    0.06746775
	DiscDate   "1999"
	Orbit
	{
		Period          6.7
		SemiMajorAxis   0.72
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "2MASS 09201+35174 B/2MASS J09201223+3517429 B"
{
	ParentBody "2MASS 09201+35174"
	Class      "L9 V"
	AppMagnJ   16.4
	AppMagnH   15.53
	AppMagnKs  13.98
	AppMagnK   14.9
	Teff       1320
	Radius     69911
	MassSol    0.06746775
	DiscDate   "2000"
	Orbit
	{
		Period          6.7
		SemiMajorAxis   0.72
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "SDSS 09261+58472 A/SDSS J092615.38+584720.9 A/2MASS J09261537+5847212 A"
{
	ParentBody "SDSS 09261+58472"
	Class      "T3.5 V"
	AppMagnJ   15.9
	AppMagnH   15.31
	AppMagnKs  15.45
	AppMagnW1  15.24
	AppMagnW2  13.66
	AppMagnW3  12.73
	MassSol    0.06936825
	DiscDate   "1997"
	Orbit
	{
		Period          17
		SemiMajorAxis   1.2044
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "SDSS 09261+58472 B/SDSS J092615.38+584720.9 B/2MASS J09261537+5847212 B"
{
	ParentBody "SDSS 09261+58472"
	Class      "T5 V"
	MassSol    0.05986575
	Orbit
	{
		Period          17
		SemiMajorAxis   1.3956
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "LP 261-75 A"
{
	ParentBody "LP 261-75"
	Class      "M4 V"
	AppMagn    15.32
	Orbit
	{
		Period          46020.6564    //Generic
		SemiMajorAxis   60.1691
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "LP 261-75 B/2MASS J09510549+3558021 B"
{
	ParentBody "LP 261-75"
	Class      "L6 V"
	AppMagnI   20.7
	AppMagnJ   17.23
	AppMagnH   15.9
	AppMagnKs  15.14
	AppMagnK   15.1
	MassSol    0.019005
	DiscDate   "1997"
	Orbit
	{
		Period          46020.6564    //Generic
		SemiMajorAxis   759.8309
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "G 196-3 A"
{
	ParentBody "G 196-3"
	Class      "M3 V"
	AppMagn    13.3
	Orbit
	{
		Period          7961.7755    //Generic
		SemiMajorAxis   16.8183
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "G 196-3 B/2MASS J10042066+5022596 B"
{
	ParentBody "G 196-3"
	Class      "L3 V"
	AppMagnR   20.78
	AppMagni   19.88
	AppMagnI   18.28
	AppMagnJ   14.83
	AppMagnH   13.65
	AppMagnKs  12.78
	AppMagnK   12.49
	Teff       1870
	Radius     88087.86
	MassSol    0.02375625
	DiscDate   "1998"
	Orbit
	{
		Period          7961.7755    //Generic
		SemiMajorAxis   283.1817
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "DENIS-P 10042-11464 A/DENIS-P J100428.3-114648 A/2MASS J10042824-1146489 A"           //Unconfirmed
{
	ParentBody "DENIS-P 10042-11464"
	Class      "M9.5 V"
	AppMagnI   18.02
	AppMagnJ   14.94
	AppMagnH   14.14
	AppMagnKs  13.61
	AppMagnK   14.85
	MassSol    0.079821
	DiscDate   "2003"
	Orbit
	{
		Period          63
		SemiMajorAxis   3.3171
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "DENIS-P 10042-11464 B/DENIS-P J100428.3-114648 B/2MASS J10042824-1146489 B"           //Unconfirmed
{
	ParentBody "DENIS-P 10042-11464"
	Class      "L0.5 V"
	MassSol    0.07602
	DiscDate   "2003"
	Orbit
	{
		Period          63
		SemiMajorAxis   3.4829
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "LHS 5166 A/L 536-140/LTT 3691/CE 126/LP 903-20"
{
	ParentBody "LHS 5166"
	Class      "M4 V"
	AppMagn    15
	Orbit
	{
		Period          3629.3446    //Generic
		SemiMajorAxis   35.8761
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "LHS 5166 B/2MASS J10043929-3335189 B"
{
	ParentBody "LHS 5166"
	Class      "L4 V"
	AppMagnJ   14.48
	AppMagnH   13.49
	AppMagnKs  12.92
	AppMagnW1  12.29
	AppMagnW2  12
	AppMagnW3  12.67
	MassSol    0.06936825
	DiscDate   "1999"
	Orbit
	{
		Period          3629.3446    //Generic
		SemiMajorAxis   124.1239
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "2MASS 10170+13083 A/2MASS J10170754+1308398 A"           //Unconfirmed
{
	ParentBody "2MASS 10170+13083"
	Class      "L1.5 V"
	AppMagni   18.54
	AppMagnI   17.8
	AppMagnJ   14.1
	AppMagnH   13.28
	AppMagnKs  12.71
	AppMagnK   13.4
	MassSol    0.07602
	DiscDate   "2003"
	Orbit
	{
		Period          23
		SemiMajorAxis   1.7
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "2MASS 10170+13083 B/2MASS J10170754+1308398 B"           //Unconfirmed
{
	ParentBody "2MASS 10170+13083"
	Class      "L3 V"
	AppMagnK   13.53
	MassSol    0.07602
	DiscDate   "2003"
	Orbit
	{
		Period          23
		SemiMajorAxis   1.7
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "HD 89707 A/HIP 50671"
{
	ParentBody "HD 89707"
	Class      "G1 V"
	AppMagn    7.17
	Orbit
	{
		Period          0.817
		SemiMajorAxis   0.0394
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "HD 89707 b/2MASS J10205001-1528483 b"           //Unconfirmed
{
	ParentBody "HD 89707"
	MassSol    0.0513135
	DiscDate   "1997"
	Orbit
	{
		Period          0.817
		SemiMajorAxis   0.8376
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "SDSS 10210-03042 A/SDSS J102109.69-030420.1 A/2MASS J10210969-0304197 A"
{
	ParentBody "SDSS 10210-03042"
	Class      "T1 V"
	AppMagnr   23.31
	AppMagni   22.39
	AppMagnJ   16.93
	AppMagnH   15.68
	AppMagnKs  15.38
	AppMagnW1  14.81
	AppMagnW2  13.77
	AppMagnW3  12.25
	MassSol    0.05986575
	DiscDate   "2000"
	Orbit
	{
		Period          48
		SemiMajorAxis   2.2609
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "SDSS 10210-03042 B/SDSS J102109.69-030420.1 B/2MASS J10210969-0304197 B"
{
	ParentBody "SDSS 10210-03042"
	Class      "T5 V"
	AppMagnJ   17.09
	AppMagnH   16.8
	AppMagnKs  16.84
	MassSol    0.049413
	DiscDate   "2006"
	Orbit
	{
		Period          48
		SemiMajorAxis   2.7391
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "HD 92320 A/HIP 42278"
{
	ParentBody "HD 92320"
	Class      "G0 V"
	AppMagn    8.38
	Orbit
	{
		Period          0.3981
		SemiMajorAxis   0.026
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "HD 92320 b/2MASS J10405690+5920330 b"           //Unconfirmed
{
	ParentBody "HD 92320"
	MassSol    0.05606475
	Orbit
	{
		Period          0.3981
		SemiMajorAxis   0.51
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "OGLE-TR-109 A"
{
	ParentBody "OGLE-TR-109"
	Class      "F0 V"
	AppMagn    15.8
	Orbit
	{
		Period          0.002
		SemiMajorAxis   0.0001
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "OGLE-TR-109 b"           //Unconfirmed
{
	ParentBody "OGLE-TR-109"
	Radius     62919.9
	MassSol    0.0133035
	DiscDate   "2002"
	Orbit
	{
		Period          0.002
		SemiMajorAxis   0.0159
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "WDS 11013-7733 A/WDS J11013-7733 A/2MASS J11011926-7732383 A"
{
	ParentBody "WDS 11013-7733"
	Class      "M7 V"
	AppMagnR   19.4
	AppMagnJ   13.1
	AppMagnH   12.22
	AppMagnKs  11.63
	MassSol    0.049413
	Orbit
	{
		Period          19500
		SemiMajorAxis   80.6333
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "WDS 11013-7733 B/WDS J11013-7733 B/2MASS J11011926-7732383 B"
{
	ParentBody "WDS 11013-7733"
	Class      "M8 V"
	MassSol    0.0247065
	Orbit
	{
		Period          19500
		SemiMajorAxis   161.2667
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "Cha H Alpha 8 A/ChaHa8"
{
	ParentBody "Cha H Alpha 8"
	Class      "M6 V"
	AppMagn    20.1
	Orbit
	{
		Period          5.19
		SemiMajorAxis   0.1958
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Cha H Alpha 8 B"
{
	ParentBody "Cha H Alpha 8"
	MassSol    0.02375625
	DiscDate   "2007"
	Orbit
	{
		Period          5.19
		SemiMajorAxis   0.8242
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "GJ 417 A/Gliese 417 A/HIP 54745 A/MN UMa/IRAS 11098+3605 A/GC 15397/2MASS J11123236+3548508 A/BD+36 2162/HD 97334 A/HR 4345"
{
	ParentBody "GJ 417"
	Class "G0 V"
	AppMagn 6.41
	Orbit
	{
		Period          82144.780647
		SemiMajorAxis   161.850342
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}
Barycenter "GJ 417 (BC)"
{
	ParentBody "GJ 417"
	Orbit
	{
		Period 			82144.780647
		SemiMajorAxis   1838.149658
		ArgOfPericenter 180
		MeanAnomaly 	0
	}
}

Star "GJ 417 B/Gliese 417 B/2MASS J11122567+3548131 B"
{
	ParentBody "GJ 417 (BC)"
	Class      "L4.5 V"
	AppMagnI   18.32
	AppMagnJ   15.05
	AppMagnH   14.19
	AppMagnKs  12.72
	AppMagnK   13.29
	AppMagnW2  11.62
	Teff       1700
	Radius     69911
	MassSol    0.05036325
	DiscDate   "2000"
	Orbit
	{
		Period          15.65
		SemiMajorAxis   1.3545
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "GJ 417 C/Gliese 417 C/2MASS J11122567+3548131 C"
{
	ParentBody "GJ 417 (BC)"
	Class      "L6 V"
	AppMagnJ   15.49
	AppMagnH   14.45
	AppMagnK   13.63
	Teff       1630
	Radius     69911
	MassSol    0.045612
	DiscDate   "2003"
	Orbit
	{
		Period          15.65
		SemiMajorAxis   1.4955
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "LHS 2397a A/LP 732-94/Gliese 3655/GJ 3655"
{
	ParentBody "LHS 2397a"
	Class      "M8 V"
	AppMagn    19.57
	Orbit
	{
		Period          14.26
		SemiMajorAxis   1.393
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "LHS 2397a B/2MASS J11202634-1440017 B"
{
	ParentBody "LHS 2397a"
	Class      "L7.5 V"
	AppMagnJ   15.23
	AppMagnH   14.4
	AppMagnK   13.6
	Teff       1350
	Radius     69911
	MassSol    0.06746775
	DiscDate   "2003"
	Orbit
	{
		Period          14.26
		SemiMajorAxis   1.693
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "CD-33 7795 A"
{
	ParentBody "CD-33 7795"
	Class      "M1 V"
	AppMagn    11.37
	Orbit
	{
		Period          1380
		SemiMajorAxis   5.9715
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "CD-33 7795 B/2MASS J11315526-3436272 B"
{
	ParentBody "CD-33 7795"
	Class      "M8.5 V"
	AppMagn    20.4
	AppMagnI   15.8
	Teff       2800
	Radius     141220.22
	MassSol    0.02565675
	DiscDate   "1999"
	Orbit
	{
		Period          1380
		SemiMajorAxis   121.0285
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "2MASS 11463+22305 A/2MASS J11463449+2230527 A"
{
	ParentBody "2MASS 11463+22305"
	Class      "L3 V"
	AppMagn    22.56
	AppMagnR   20.14
	AppMagni   18.85
	AppMagnI   17.62
	AppMagnJ   14.17
	AppMagnH   13.18
	AppMagnKs  12.59
	MassSol    0.05986575
	DiscDate   "1999"
	Orbit
	{
		Period          94
		SemiMajorAxis   3.95
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "2MASS 11463+22305 B/2MASS J11463449+2230527 B"
{
	ParentBody "2MASS 11463+22305"
	Class      "L3 V"
	MassSol    0.05986575
	DiscDate   "1999"
	Orbit
	{
		Period          94
		SemiMajorAxis   3.95
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "2MASSI 12171-03111 A/2MASSI J1217110-0311131 A/2MASS J12171110-0311131 A"
{
	ParentBody "2MASSI 12171-03111"
	Class      "T7.5 V"
	AppMagnr   23.92
	AppMagni   22.87
	AppMagnI   21.53
	AppMagnJ   15.86
	AppMagnH   15.75
	AppMagnKs  15.89
	AppMagnK   15.92
	AppMagnW1  15.38
	AppMagnW2  13.2
	AppMagnW3  11.59
	Teff       870
	Radius     66415.45
	MassSol    0.02945775
	DiscDate   "1999"
	Orbit
	{
		Period          15.6515    //Generic
		SemiMajorAxis   0.9288
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "2MASSI 12171-03111 B/2MASSI J1217110-0311131 B/2MASS J12171110-0311131 B"
{
	ParentBody "2MASSI 12171-03111"
	Class      "T7 V"
	Teff       860
	Radius     65017.23
	MassSol    0.01995525
	DiscDate   "1999"
	Orbit
	{
		Period          15.6515    //Generic
		SemiMajorAxis   1.3712
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "2MASS 12255-27394 B/2MASS J12255432-2739466 B"
{
	ParentBody "2MASS 12255-27394"
	Class      "T8 V"
	AppMagnJ   16.92
	AppMagnH   16.92
	AppMagnK   16.73
	Teff       675
	MassSol    0.03325875
	DiscDate   "1999"
	Orbit
	{
		Period          23
		SemiMajorAxis   1.5833
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "2MASS 12255-27394 A/2MASS J12255432-2739466 A"
{
	ParentBody "2MASS 12255-27394"
	Class      "T6 V"
	AppMagnI   20.32
	AppMagnJ   15.26
	AppMagnH   15.1
	AppMagnKs  15.07
	AppMagnK   15.38
	AppMagnW1  14.66
	AppMagnW2  12.69
	AppMagnW3  11.16
	MassSol    0.02375625
	DiscDate   "1999"
	Orbit
	{
		Period          23
		SemiMajorAxis   2.2167
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "DENIS-P 12281-15473 B/DENIS-P J122815.2-154733 B/2MASS J12281523-1547342 B"
{
	ParentBody "DENIS-P 12281-15473"
	Class      "L5 V"
	AppMagnKs  13.59
	MassSol    0.05986575
	DiscDate   "1997"
	Orbit
	{
		Period          44
		SemiMajorAxis   3.2
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "DENIS-P 12281-15473 A/DENIS-P J122815.2-154733 A/2MASS J12281523-1547342 A"
{
	ParentBody "DENIS-P 12281-15473"
	Class      "L5 V"
	AppMagnR   20.48
	AppMagnI   18.22
	AppMagnJ   14.38
	AppMagnH   13.35
	AppMagnKs  13.45
	AppMagnK   12.74
	AppMagnW1  12.01
	AppMagnW2  11.68
	AppMagnW3  11
	MassSol    0.05986575
	DiscDate   "1997"
	Orbit
	{
		Period          44
		SemiMajorAxis   3.2
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "2MASS 12392+55153 A/2MASS J12392727+5515371 A"
{
	ParentBody "2MASS 12392+55153"
	Class      "L5 V"
	AppMagni   19.64
	AppMagnI   18.6
	AppMagnJ   14.71
	AppMagnH   13.57
	AppMagnKs  12.79
	MassSol    0.0703185
	DiscDate   "2000"
	Orbit
	{
		Period          35
		SemiMajorAxis   2.25
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "2MASS 12392+55153 B/2MASS J12392727+5515371 B"
{
	ParentBody "2MASS 12392+55153"
	Class      "L5 V"
	MassSol    0.0703185
	DiscDate   "2003"
	Orbit
	{
		Period          35
		SemiMajorAxis   2.25
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}
Barycenter "Kelu-1 A"
{
	ParentBody "Kelu-1"
	Orbit
	{
		Period          52
		SemiMajorAxis   1.7022
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Kelu-1 Aa/2MASS J13054019-2541059 a"
{
	ParentBody "Kelu-1 A"
	Class      "L2 V"
	AppMagn    21.77
	AppMagnR   19.1
	AppMagnI   16.94
	AppMagnJ   13.41
	AppMagnH   12.39
	AppMagnKs  11.75
	AppMagnK   11.8
	AppMagnW1  11.25
	AppMagnW2  10.92
	AppMagnW3  10.37
	Teff       2100
	Radius     83893.2
	MassSol    0.05986575
	DiscDate   "1997"
	Orbit
	{
		Period          23.0607    //Generic
		SemiMajorAxis   2
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Kelu-1 Ab/2MASS J13054019-2541059 b"
{
	ParentBody "Kelu-1 A"
	Class      "T7.5 V"
	DiscDate   "2005"
	Orbit
	{
		Period          23.0607    //Generic
		SemiMajorAxis   2
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "Kelu-1 B/2MASS J13054019-2541059 B"
{
	ParentBody "Kelu-1"
	Class      "L4 V"
	AppMagnJ   13.41
	AppMagnH   12.39
	AppMagnKs  11.75
	MassSol    0.0551145
	DiscDate   "1997"
	Orbit
	{
		Period          52
		SemiMajorAxis   3.6978
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "2MASS 13153-26495 A/2MASS J13153094-2649513 A"
{
	ParentBody "2MASS 13153-26495"
	Class      "L3.5 V"
	AppMagnR   7.70
	AppMagnJ   15.14
	AppMagnH   14.14
	AppMagnKs  13.46
	AppMagnK   13.45
	AppMagnW1  12.73
	AppMagnW2  12.28
	AppMagnW3  11.87
	Teff       1760
	Radius     58725.24
	MassSol    0.0665175
	DiscDate   "2002"
	Orbit
	{
		Period          52
		SemiMajorAxis   1.8857
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "2MASS 13153-26495 B/2MASS J13153094-2649513 B"
{
	ParentBody "2MASS 13153-26495"
	Class      "T7 V"
	AppMagnJ   18.2
	AppMagnH   18.66
	AppMagnKs  18.79
	Teff       790
	Radius     63619.01
	MassSol    0.026607
	DiscDate   "2011"
	Orbit
	{
		Period          52
		SemiMajorAxis   4.7143
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "LHS 2722 A/G 62-33/PLX 3051/BD+04 2729/LTT 13876/HD 116012/SAO 119891/HIP 65121 A"
{
	ParentBody "LHS 2722"
	Class      "K2 V"
	AppMagn    8.59
	Orbit
	{
		Period          106738.3415    //Generic
		SemiMajorAxis   188.8602
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "2MASS 13204+04090/2MASS J13204427+0409045"           //Unconfirmed
{
	ParentBody "LHS 2722"
	Class      "L3 V"
	AppMagnJ   15.25
	AppMagnH   14.3
	AppMagnKs  13.62
	MassSol    0.079821
	DiscDate   "2004"
	Orbit
	{
		Period          106738.3415    //Generic
		SemiMajorAxis   2011.1398
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "HD 119445 A/HIP 66892"
{
	ParentBody "HD 119445"
	Class      "G6 III"
	AppMagn    6.3
	Orbit
	{
		Period          1.12
		SemiMajorAxis   0.0139
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "HD 119445 b/2MASS J13422879+4140274 b"
{
	ParentBody "HD 119445"
	MassSol    0.0361095
	DiscDate   "2009"
	Orbit
	{
		Period          1.12
		SemiMajorAxis   1.6961
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "LHS 2803 A/LP 738-14/NLTT 35266/DEA 1A"
{
	ParentBody "LHS 2803"
	Class      "M4 V"
	Orbit
	{
		Period          88163.4442    //Generic
		SemiMajorAxis   185.0728
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "LHS 2803 B/2MASS J13480290-1344071 B"
{
	ParentBody "LHS 2803"
	Class      "T5.5 V"
	AppMagnJ   16.48
	AppMagnH   16.09
	AppMagnKs  17.01
	AppMagnK   16.45
	AppMagnW1  16.15
	AppMagnW2  14.18
	AppMagnW3  12.14
	Teff       1030
	MassSol    0.0399105
	DiscDate   "2012"
	Orbit
	{
		Period          88163.4442    //Generic
		SemiMajorAxis   1112.9272
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "2MASS 14044-31593 A/2MASS J14044941-3159329 A"
{
	ParentBody "2MASS 14044-31593"
	Class      "T1 V"
	AppMagnJ   16.63
	AppMagnH   15.49
	AppMagnKs  14.85
	AppMagnW1  13.82
	AppMagnW2  12.91
	AppMagnW3  11.64
	MassSol    0.0399105
	DiscDate   "2007"
	Orbit
	{
		Period          32
		SemiMajorAxis   1.3164
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "2MASS 14044-31593 B/2MASS J14044941-3159329 B"
{
	ParentBody "2MASS 14044-31593"
	Class      "T5 V"
	AppMagnJ   16.1
	AppMagnH   15.97
	AppMagnKs  16.05
	MassSol    0.02945775
	Orbit
	{
		Period          32
		SemiMajorAxis   1.7836
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "WT460 A/2MASS J14115998-4132211 A"
{
	ParentBody "WT460"
	Class      "M6 V"
	Orbit
	{
		Period          40
		SemiMajorAxis   2.4551
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "2MASS 14115-41322 B/2MASS J14115998-4132211 B"
{
	ParentBody "WT460"
	Class      "L1 V"
	MassSol    0.07126875
	Orbit
	{
		Period          40
		SemiMajorAxis   3.4449
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "HD 126053 A/HIP 70319 A"
{
	ParentBody "HD 126053"
	Class      "G1 V"
	AppMagn    6.25
	Orbit
	{
		Period          140000
		SemiMajorAxis   77.8721
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "HD 126053 B/2MASS J14231528+0114294 B"
{
	ParentBody "HD 126053"
	Class      "T8 V"
	AppMagnJ   18.71
	AppMagnH   19.14
	AppMagnK   19.89
	AppMagnW1  18.01
	AppMagnW2  14.85
	AppMagnW3  12.66
	Teff       680
	Radius     6291.99
	MassSol    0.03325875
	DiscDate   "2012"
	Orbit
	{
		Period          140000
		SemiMajorAxis   2552.1279
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "2MASS 14263+15570 A/2MASS J14263161+1557012 A"           //Unconfirmed
{
	ParentBody "2MASS 14263+15570"
	Class      "M8.5 V"
	AppMagnI   16.5
	AppMagnJ   13.36
	AppMagnH   12.63
	AppMagnKs  12.2
	AppMagnK   12.07
	Teff       2400
	Radius     95778.07
	MassSol    0.08267175
	DiscDate   "2003"
	Orbit
	{
		Period          1985
		SemiMajorAxis   20.4639
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "2MASS 14263+15570 B/2MASS J14263161+1557012 B"
{
	ParentBody "2MASS 14263+15570"
	Class      "L1 V"
	AppMagnJ   14.13
	AppMagnH   13.34
	AppMagnKs  12.8
	AppMagnK   12.64
	Teff       2240
	Radius     78300.32
	MassSol    0.07506975
	DiscDate   "2003"
	Orbit
	{
		Period          1985
		SemiMajorAxis   22.5361
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "2MASSW 14304+29154 A/2MASSW J1430436+291541 A/2MASS J14304358+2915405 A"           //Unconfirmed
{
	ParentBody "2MASSW 14304+29154"
	Class      "L2 V"
	AppMagni   18.76
	AppMagnI   18
	AppMagnJ   14.27
	AppMagnH   13.44
	AppMagnKs  12.77
	AppMagnW1  12.33
	AppMagnW2  12
	AppMagnW3  11.2
	MassSol    0.07602
	DiscDate   "2003"
	Orbit
	{
		Period          15
		SemiMajorAxis   1.2918
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "2MASSW 14304+29154 B/2MASSW J1430436+291541 B/2MASS J14304358+2915405 B"
{
	ParentBody "2MASSW 14304+29154"
	Class      "L3 V"
	MassSol    0.07506975
	DiscDate   "2003"
	Orbit
	{
		Period          15
		SemiMajorAxis   1.3082
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "G 124-62 A/HIP 54745/HD 97334"
{
	ParentBody "G 124-62"
	Class "M5 V"
	Orbit
	{
		Period 			124327.004281
		SemiMajorAxis 	469.865121
		ArgOfPericenter 0
		MeanAnomaly 	0
	}
}

Barycenter "G 124-62 B"
{
	ParentBody "G 124-62"
	Orbit
	{
		Period 			124327.004281
		SemiMajorAxis 	1030.134879
		ArgOfPericenter 180
		MeanAnomaly 	0
	}
}

Star "G 124-62 Bb/2MASS J14413716-0945590 Bb"
{
	ParentBody "G 124-62 B"
	Class      "L0.5 V"
	AppMagnW2  12.08
	MassSol    0.034209
	DiscDate   "2005"
	Orbit
	{
		Period          200
		SemiMajorAxis   7.15
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "G 124-62 Ba/2MASS J14413716-0945590 Ba"
{
	ParentBody "G 124-62 B"
	Class      "L1 V"
	AppMagnR   19.6
	AppMagnI   17.41
	AppMagnJ   14.02
	AppMagnH   13.19
	AppMagnKs  12.66
	MassSol    0.034209
	DiscDate   "1999"
	Orbit
	{
		Period          200
		SemiMajorAxis   7.15
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "G 239-25 A/HIC 71898/WDS J14424+6603AB/MCC 723"
{
	ParentBody "G 239-25"
	Class      "M2 V"
	AppMagn    10.83
	Orbit
	{
		Period          216.8442    //Generic
		SemiMajorAxis   3.7427
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "G 239-25 B/2MASS J14422164+6603208 B"
{
	ParentBody "G 239-25"
	Class      "L0 V"
	AppMagnJ   11.51
	AppMagnH   10.83
	AppMagnKs  10.33
	MassSol    0.07126875
	DiscDate   "2004"
	Orbit
	{
		Period          216.8442    //Generic
		SemiMajorAxis   26.2573
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "2MASS 14493+23553 A/2MASS J14493784+2355378 A"           //Unconfirmed
{
	ParentBody "2MASS 14493+23553"
	Class      "L0 V"
	AppMagni   19.93
	AppMagnI   18.9
	AppMagnJ   15.82
	AppMagnH   15
	AppMagnKs  14.31
	AppMagnW1  14.24
	AppMagnW2  14.53
	AppMagnW3  12.47
	MassSol    0.083622
	DiscDate   "2000"
	Orbit
	{
		Period          88
		SemiMajorAxis   4.021
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "2MASS 14493+23553 B/2MASS J14493784+2355378 B"
{
	ParentBody "2MASS 14493+23553"
	Class      "L3 V"
	MassSol    0.07506975
	DiscDate   "2003"
	Orbit
	{
		Period          88
		SemiMajorAxis   4.479
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "HD 130948 A"
{
	ParentBody "HD 130948"
	Class      "G2 V"
	AppMagn    5.86
	Orbit
	{
		Period          306.115507
		SemiMajorAxis   4.5938
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}
Barycenter "HD 130948 (BC)"
{
	ParentBody "HD 130948"
	Orbit
	{
		Period          306.115507
		SemiMajorAxis   42.4062
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "HD 130948 B/2MASS J14501581+2354424 B"
{
	ParentBody "HD 130948 (BC)"
	Class      "L4 V"
	AppMagnJ   13.81
	AppMagnH   13.04
	AppMagnKs  12.3
	AppMagnK   12.26
	Teff       1840
	Radius     76202.99
	MassSol    0.0551145
	DiscDate   "2002"
	Orbit
	{
		Period          9.83
		SemiMajorAxis   1.0748
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "HD 130948 C/2MASS J14501581+2354424 C"
{
	ParentBody "HD 130948 (BC)"
	Class      "L4 V"
	AppMagnJ   14.12
	AppMagnH   13.33
	AppMagnKs  12.6
	AppMagnK   12.46
	Teff       1790
	Radius     71309.22
	MassSol    0.053214
	DiscDate   "2002"
	Orbit
	{
		Period          9.83
		SemiMajorAxis   1.1132
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "GJ 569 A/CE Boo/Gliese 569/G 136-28/MCC 147/BD+16 2708/HIP 72944"
{
	ParentBody "GJ 569"
	Class      "M2 V"
	AppMagn    10.15
	Orbit
	{
		Period          442.124033
		SemiMajorAxis   10.918371
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}
Barycenter "GJ 569 BC"
{
	ParentBody "GJ 569"
	Orbit
	{
		Period          442.124033
		SemiMajorAxis   39.081629
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "GJ 569 B/2MASS J14542923+1606039 B"
{
	ParentBody "GJ 569 BC"
	Class      "M8.5 V"
	AppMagn    18.1
	AppMagnJ   11.27
	AppMagnH   10.67
	AppMagnK   10.16
	Teff       2530
	Radius     68512.78
	MassSol    0.0741195
	DiscDate   "2000"
	Orbit
	{
		Period          2.367
		SemiMajorAxis   0.4332
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "GJ 569 C/2MASS J14542923+1606039 C"
{
	ParentBody "GJ 569 BC"
	Class      "M9 V"
	AppMagnJ   11.78
	AppMagnH   11.21
	AppMagnK   10.64
	Teff       2300
	Radius     66415.45
	MassSol    0.06556725
	DiscDate   "2000"
	Orbit
	{
		Period          2.367
		SemiMajorAxis   0.4898
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "GJ 576 A/Gliese 576 A/LHS 3020/G 15-5/LTT 14482/MCC 158/HIP 73786"
{
	ParentBody "GJ 576"
	Class      "K8 V"
	AppMagn    9.815
	Orbit
	{
		Period          56099.8945    //Generic
		SemiMajorAxis   24.6146
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "GJ 576 B"
{
	ParentBody "GJ 576"
	Class      "T6.5 V"
	AppMagnr   25.5
	AppMagni   23.84
	AppMagnJ   16.59
	AppMagnH   17.05
	AppMagnK   17.41
	AppMagnW1  16.48
	AppMagnW2  14.23
	Teff       975
	DiscDate   "2010"
	Orbit
	{
		Period          56099.8945    //Generic
		SemiMajorAxis   1235.3854
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "HD 136118 A/HIP 74948 A"
{
	ParentBody "HD 136118"
	Class      "F8 V"
	AppMagn    6.93
	Orbit
	{
		Period          3.251
		SemiMajorAxis   0.0278
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "HD 136118 b"           //Unconfirmed
{
	ParentBody "HD 136118"
	MassSol    0.01425375
	DiscDate   "2002"
	Orbit
	{
		Period          3.251
		SemiMajorAxis   2.3022
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "2MASS 15200-44224 A/2MASS J15200224-4422419 A"           //Unconfirmed
{
	ParentBody "2MASS 15200-44224"
	Class      "L1.5 V"
	AppMagnI   16.78
	AppMagnJ   13.55
	AppMagnH   12.73
	AppMagnKs  12.27
	MassSol    0.0817215
	DiscDate   "2003"
	Orbit
	{
		Period          400
		SemiMajorAxis   10.6707
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "2MASS 15200-44224 B/2MASS J15200224-4422419 B"           //Unconfirmed
{
	ParentBody "2MASS 15200-44224"
	Class      "L4.5 V"
	AppMagnJ   14.7
	AppMagnH   13.7
	AppMagnKs  13.22
	MassSol    0.07697025
	DiscDate   "2007"
	Orbit
	{
		Period          400
		SemiMajorAxis   11.3293
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//ETA CrB;english and spanish wiki
//very good system

Barycenter "ETA CrB (AB)"
{
	ParentBody "ETA CrB"
	Orbit
	{
		Period          142065.4963    //Generic
		SemiMajorAxis   93.7084
		Inclination     58.084
		AscendingNode   202.827
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "ETA CrB A/GJ 584 A/Gliese 584 A/HIP 75312/HD 137107"
{
	ParentBody "ETA CrB (AB)"
	Class 	   "G1 V"
	AppMagn    5.64
	MassSol    1.19
	Orbit
	{
		Period          41.6287
		SemiMajorAxis   7.191
		Eccentricity    0.27907
		Inclination     58.084
		AscendingNode   202.827
		ArgOfPericenter 39.885
		Epoch           2442612.9
		MeanAnomaly     0
	}
}

Star "ETA CrB B/GJ 584 B/Gliese 584 B"
{
	ParentBody "ETA CrB (AB)"
	Class "G3 V"
	AppMagn 5.95
	MassSol 1.05
	Orbit
	{
		Period 			41.6287
		SemiMajorAxis 	8.1498
		Eccentricity 	0.27907
		Inclination 	58.084
		AscendingNode 	202.827
		ArgOfPericenter 219.885
		Epoch 			2442612.9
		MeanAnomaly 	0
	}
}

Star "ETA CrB C/GJ 584 C/Gliese 584 C/2MASS J15232263+3014562 C"
{
	ParentBody "ETA CrB"
	Class      "L8 V"
	AppMagn    24.4
	AppMagnI   20.27
	AppMagnJ   16.06
	AppMagnH   14.93
	AppMagnKs  14.35
	AppMagnK   14.31
	AppMagnW1  13.56
	AppMagnW2  12.99
	AppMagnW3  11.92
	MassSol    0.05986575
	DiscDate   "2000"
	Orbit
	{
		Period          142065.4963    //Generic
		SemiMajorAxis   3506.2916
		Inclination     58.084
		AscendingNode   202.827
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "HD 137510 A/HIP 75535 A"
{
	ParentBody "HD 137510"
	Class      "G0 V"
	AppMagn    6.26
	Orbit
	{
		Period          2.1938
		SemiMajorAxis   0.0673
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "HD 137510 b/2MASS J15255328+1928505 b"
{
	ParentBody "HD 137510"
	MassSol    0.04086075
	DiscDate   "2004"
	Orbit
	{
		Period          2.1938
		SemiMajorAxis   1.8127
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "SDSS 15341+16154 A/SDSS J153417.05+161546.1 A/2MASS J15341711+1615463 A"
{
	ParentBody "SDSS 15341+16154"
	Class      "T0 V"
	AppMagnI   19.62
	AppMagnJ   16.75
	AppMagnH   16.08
	AppMagnKs  16.41
	AppMagnK   16.06
	AppMagnW1  15.44
	AppMagnW2  14.41
	AppMagnW3  12.52
	MassSol    0.03515925
	DiscDate   "2000"
	Orbit
	{
		Period          3.7614    //Generic
		SemiMajorAxis   0.5
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "SDSS 15341+16154 B/SDSS J153417.05+161546.1 B/2MASS J15341711+1615463 B"
{
	ParentBody "SDSS 15341+16154"
	Class      "T5.5 V"
	AppMagnH   17.53
	MassSol    0.03515925
	DiscDate   "2006"
	Orbit
	{
		Period          3.7614    //Generic
		SemiMajorAxis   0.5
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "2MASS 15344-29522 B/2MASS J15344984-2952274 B"
{
	ParentBody "2MASS 15344-29522"
	Class      "T6 V"
	AppMagnJ   15.44
	AppMagnH   15.81
	AppMagnKs  15.03
	AppMagnK   15.7
	Teff       1097
	Radius     55928.8
	MassSol    0.026607
	DiscDate   "2002"
	Orbit
	{
		Period          23.1
		SemiMajorAxis   1.6448
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "2MASS 15344-29522 A/2MASS J15344984-2952274 A"
{
	ParentBody "2MASS 15344-29522"
	Class      "T5.5 V"
	AppMagnJ   15.28
	AppMagnH   15.46
	AppMagnKs  14.84
	AppMagnK   15.51
	AppMagnW1  13.94
	AppMagnW2  12.59
	AppMagnW3  11.59
	Teff       1130
	Radius     55928.8
	MassSol    0.0285075
	DiscDate   "2002"
	Orbit
	{
		Period          23.1
		SemiMajorAxis   1.5352
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "2MASS 15500+14551 A/2MASS J15500845+1455180 A"
{
	ParentBody "2MASS 15500+14551"
	Class      "L3.5 V"
	AppMagni   19.75
	AppMagnJ   14.78
	AppMagnH   14.42
	AppMagnKs  13.26
	Teff       1910
	MassSol    0.06936825
	DiscDate   "2000"
	Orbit
	{
		Period          444.6118    //Generic
		SemiMajorAxis   14.6853
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "2MASS 15500+14551 B/2MASS J15500845+1455180 B"
{
	ParentBody "2MASS 15500+14551"
	Class      "L4 V"
	AppMagni   20.56
	AppMagnJ   15.76
	AppMagnH   14.7
	AppMagnKs  14.13
	Teff       1840
	MassSol    0.0665175
	DiscDate   "2009"
	Orbit
	{
		Period          444.6118    //Generic
		SemiMajorAxis   15.3147
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "2MASS 15530+15323 A/2MASS J15530228+1532369 A"
{
	ParentBody "2MASS 15530+15323"
	Class      "T6.5 V"
	AppMagnJ   16.41
	AppMagnH   16.52
	AppMagnKs  15.51
	AppMagnK   16.07
	AppMagnW1  15.28
	AppMagnW2  13.02
	AppMagnW3  12.02
	Teff       941
	MassSol    0.0399105
	DiscDate   "1999"
	Orbit
	{
		Period          44
		SemiMajorAxis   1.9671
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "2MASS 15530+15323 B/2MASS J15530228+1532369 B"
{
	ParentBody "2MASS 15530+15323"
	Class      "T7 V"
	AppMagnJ   16.77
	AppMagnH   16.9
	AppMagnK   16.5
	Teff       825
	MassSol    0.03515925
	DiscDate   "2006"
	Orbit
	{
		Period          44
		SemiMajorAxis   2.2329
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "2MASS 16000+17083 A/2MASS J16000548+1708328 A"           //Unconfirmed
{
	ParentBody "2MASS 16000+17083"
	Class      "L1 V"
	AppMagnI   19.3
	AppMagnJ   16.05
	AppMagnH   15.11
	AppMagnKs  14.68
	MassSol    0.0779205
	DiscDate   "2000"
	Orbit
	{
		Period          23
		SemiMajorAxis   1.7174
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "2MASS 16000+17083 B/2MASS J16000548+1708328 B"
{
	ParentBody "2MASS 16000+17083"
	Class      "L3 V"
	MassSol    0.07506975
	DiscDate   "2003"
	Orbit
	{
		Period          23
		SemiMajorAxis   1.7826
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "UScoCTIO 109 B"
{
	ParentBody "UScoCTIO 109"
	Class      "M7.5 V"
	MassSol    0.0399105
	Orbit
	{
		Period          46
		SemiMajorAxis   3.1104
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "UScoCTIO 109 A/2MASS J16011915-2306394 A"
{
	ParentBody "UScoCTIO 109"
	Class      "M6 V"
	AppMagnR   17.9
	AppMagnJ   13.61
	AppMagnH   13.04
	AppMagnKs  12.67
	MassSol    0.06936825
	Orbit
	{
		Period          46
		SemiMajorAxis   1.7896
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "UScoCTIO 66 A/2MASS J16014955-2351082 A"
{
	ParentBody "UScoCTIO 66"
	Class      "M6 V"
	AppMagnR   17
	AppMagnJ   12.91
	AppMagnH   12.29
	AppMagnKs  11.93
	Teff       2928
	MassSol    0.06936825
	Orbit
	{
		Period          122
		SemiMajorAxis   5.095
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "UScoCTIO 66 B/2MASS J16014955-2351082 B"           //Unconfirmed
{
	ParentBody "UScoCTIO 66"
	Class      "M6 V"
	AppMagnR   17
	AppMagnJ   12.91
	AppMagnH   12.29
	AppMagnKs  11.93
	Teff       2928
	MassSol    0.06936825
	Orbit
	{
		Period          122
		SemiMajorAxis   5.095
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "UScoCTIO 55 A"
{
	ParentBody "UScoCTIO 55"
	Class      "M5.5 V"
	Orbit
	{
		Period          255
		SemiMajorAxis   5.5749
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "UScoCTIO 55 B"           //Unconfirmed
{
	ParentBody "UScoCTIO 55"
	Class      "M6 V"
	AppMagnJ   12.46
	AppMagnH   11.84
	AppMagnKs  11.5
	MassSol    0.06936825
	Orbit
	{
		Period          255
		SemiMajorAxis   12.0551
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "LSR 1610-0040 A/LSR J1610-0040 A/2MASS J16102900-0040530 A/USND-B1.0 0893-00162897"
{
	ParentBody "LSR 1610-0040"
	Class      "M7 V"
	AppMagn    12.91
	Orbit
	{
		Period          1.662
		SemiMajorAxis   0.1532
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "LSR 1610-0040 B/LSR J1610-0040 B/2MASS J16102900-0040530 B"
{
	ParentBody "LSR 1610-0040"
	Class      "L V"
	AppMagnr   17.97
	AppMagnR   17.51
	AppMagni   15.89
	AppMagnI   14.81
	AppMagnJ   12.91
	AppMagnH   12.3
	AppMagnKs  12.02
	AppMagnW1  11.64
	AppMagnW2  11.54
	Teff       1900
	MassSol    0.05986575
	DiscDate   "2003"
	Orbit
	{
		Period          1.662
		SemiMajorAxis   0.2508
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "SIMP 16192+03135 A/SIMP J1619275+031350 A/2MASS J16192751+0313507 A"
{
	ParentBody "SIMP 16192+03135"
	Class      "T2.5 V"
	AppMagni   23.11
	AppMagnJ   15.85
	AppMagnH   15.48
	AppMagnKs  15.49
	Teff       1219
	DiscDate   "2011"
	Orbit
	{
		Period          175
		SemiMajorAxis   7.7
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "SIMP 16192+03135 B/SIMP J1619275+031350 B/2MASS J16192751+0313507 B"
{
	ParentBody "SIMP 16192+03135"
	Class      "T4 V"
	AppMagni   23.41
	AppMagnJ   16.62
	AppMagnH   15.89
	AppMagnKs  15.9
	Teff       1164
	DiscDate   "2011"
	Orbit
	{
		Period          175
		SemiMajorAxis   7.7
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "GJ 618.1 A/Gliese 618.1 A/GJ 9555/LTT 6518/MCC 760/UGP 404/G 153-44/HIP 80053"
{
	ParentBody "GJ 618.1"
	Class      "M2 V"
	AppMagn    10.71
	Orbit
	{
		Period          48850.0446    //Generic
		SemiMajorAxis   80.5734
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "GJ 618.1 B/2MASS J16202614-0416315 B"
{
	ParentBody "GJ 618.1"
	Class      "L2.5 V"
	AppMagnJ   15.28
	AppMagnH   14.35
	AppMagnKs  13.6
	AppMagnK   13.59
	MassSol    0.0399105
	DiscDate   "2001"
	Orbit
	{
		Period          48850.0446    //Generic
		SemiMajorAxis   1009.4266
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "HD 149382 A/HIP 81145 A"
{
	ParentBody "HD 149382"
	Class      "B5 V"
	AppMagn    8.94
	Orbit
	{
		Period          0.00655
		SemiMajorAxis   0.0001
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "HD 149382 b/2MASS J16342334-0400521 b"           //Unconfirmed
{
	ParentBody "HD 149382"
	MassSol    0.015204
	DiscDate   "2009"
	Orbit
	{
		Period          0.00655
		SemiMajorAxis   0.0255
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "2MASS 17072-05582 A"
{
	ParentBody "2MASS 17072-05582"
	Class      "M9 V"
	Orbit
	{
		Period          210
		SemiMajorAxis   8.2408
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "2MASS 17072-05582 B/2MASS J17072343-0558249 B"           //Unconfirmed
{
	ParentBody "2MASS 17072-05582"
	Class      "L3 V"
	AppMagnJ   13.96
	AppMagnH   12.72
	AppMagnKs  12.2
	MassSol    0.07697025
	DiscDate   "2003"
	Orbit
	{
		Period          210
		SemiMajorAxis   6.9592
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "WISEPA 17110+35003 B/WISEPA J171104.60+350036.8 B"
{
	ParentBody "WISEPA 17110+35003"
	Class      "T9.5 V"
	AppMagnJ   20.5
	AppMagnH   20.96
	AppMagnK   21.38
	Teff       480
	MassSol    0.01615425
	DiscDate   "2012"
	Orbit
	{
		Period          700
		SemiMajorAxis   9.9
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "WISEPA 17110+35003 A/WISEPA J171104.60+350036.8 A"
{
	ParentBody "WISEPA 17110+35003"
	Class      "T8 V"
	AppMagnJ   17.67
	AppMagnH   18.13
	AppMagnK   18.3
	AppMagnW1  18.27
	AppMagnW2  14.61
	AppMagnW3  12.72
	Teff       770
	MassSol    0.03135825
	DiscDate   "2011"
	Orbit
	{
		Period          700
		SemiMajorAxis   5.1
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "G 203-50 A"
{
	ParentBody "G 203-50"
	Class      "M4.5 V"
	AppMagn    15
	Orbit
	{
		Period          2830.3058    //Generic
		SemiMajorAxis   28.9677
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "G 203-50 B/2MASS J17114559+4028578 B"
{
	ParentBody "G 203-50"
	Class      "L5 V"
	AppMagnJ   15
	AppMagnH   14.3
	AppMagnKs  13.8
	Teff       1700
	MassSol    0.06556725
	DiscDate   "2008"
	Orbit
	{
		Period          2830.3058    //Generic
		SemiMajorAxis   106.0323
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "GJ 660.1 A/Gliese 660.1 A/LHS 3271/LP 687-10/PLX 3902/GJ 9588/G 19-16"
{
	ParentBody "GJ 660.1"
	Class      "M4.5 V"
	AppMagn    11.622
	Orbit
	{
		Period          938.7483    //Generic
		SemiMajorAxis   60.5
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "GJ 660.1 B/Gliese 660.1 B/2MASS J17125127-0507311 B"           //Unconfirmed
{
	ParentBody "GJ 660.1"
	Class      "M9 V"
	AppMagnJ   13.05
	AppMagnH   12.57
	AppMagnK   12.23
	DiscDate   "2011"
	Orbit
	{
		Period          938.7483    //Generic
		SemiMajorAxis   60.5
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "2MASS 17281+39485 A/2MASS J17281150+3948593 A"
{
	ParentBody "2MASS 17281+39485"
	Class      "L7 V"
	AppMagnI   19.6
	AppMagnJ   16.59
	AppMagnH   15.31
	AppMagnKs  13.91
	AppMagnK   14.38
	AppMagnW1  13.11
	AppMagnW2  12.6
	AppMagnW3  11.87
	Teff       1450
	Radius     69911
	MassSol    0.068418
	DiscDate   "2000"
	Orbit
	{
		Period          31.3
		SemiMajorAxis   2.5936
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "2MASS 17281+39485 B/2MASS J17281150+3948593 B"
{
	ParentBody "2MASS 17281+39485"
	Class      "L8 V"
	AppMagnJ   16.91
	AppMagnH   15.76
	AppMagnK   15.04
	Teff       1280
	Radius     69911
	MassSol    0.06556725
	DiscDate   "2003"
	Orbit
	{
		Period          31.3
		SemiMajorAxis   2.7064
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "TYC 2087-00255-1 A/TYC 2087-255-1 A/GSC 9287-00255"
{
	ParentBody "TYC 2087-00255-1"
	Class      "G0 IV"
	AppMagn    10.5
	Orbit
	{
		Period          0.02466
		SemiMajorAxis   0.003
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "TYC 2087-00255 B/TYC 2087-00255-1 B/2MASS J17343374+2958056 B"           //Unconfirmed
{
	ParentBody "TYC 2087-00255-1"
	MassSol    0.03801
	DiscDate   "2013"
	Orbit
	{
		Period          0.02466
		SemiMajorAxis   0.087
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "LSPM 1735+2634 A/LSPM 1735+2634 A/2MASS J17351296+2634475 A"           //Unconfirmed
{
	ParentBody "LSPM 1735+2634"
	Class      "M7.5 V"
	AppMagnR   18.1
	AppMagnI   14.2
	AppMagnJ   11.76
	AppMagnH   11.1
	AppMagnKs  10.16
	AppMagnK   10.69
	MassSol    0.0817215
	DiscDate   "2009"
	Orbit
	{
		Period          20
		SemiMajorAxis   1.522
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "LSPM 1735+2634 B/LSPM 1735+2634 B/2MASS J17351296+2634475 B"
{
	ParentBody "LSPM 1735+2634"
	Class      "L0 V"
	AppMagnJ   12.33
	AppMagnH   11.66
	AppMagnK   11.18
	MassSol    0.0741195
	Orbit
	{
		Period          20
		SemiMajorAxis   1.678
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "G 259-20 A/NLTT 45697/AC+85 1711/LTT 18464"
{
	ParentBody "G 259-20"
	AppMagn    11.99
	Orbit
	{
		Period          16382.4978    //Generic
		SemiMajorAxis   24.2694
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "2MASS 17430+85265/2MASS J17430860+8526594"
{
	ParentBody "G 259-20"
	Class      "L5 V"
	AppMagnJ   14.56
	AppMagnH   13.82
	AppMagnKs  13.47
	AppMagnW1  12.88
	AppMagnW2  12.53
	AppMagnW3  12.40
	MassSol    0.03801
	DiscDate   "2012"
	Orbit
	{
		Period          16382.4978    //Generic
		SemiMajorAxis   625.7306
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "LSPM J175+4424 A"
{
	ParentBody "LSPM J175+4424"
	Class      "M7.5 V"
	Orbit
	{
		Period          44819.8831    //Generic
		SemiMajorAxis   181.6521
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "2MASSW 17501+44240 B/2MASSW J1750129+442404 B/2MASS J17501291+4424043 B"           //Unconfirmed
{
	ParentBody "LSPM J175+4424"
	Class      "L0 V"
	AppMagnJ   14.14
	AppMagnH   13.37
	AppMagnK   12.91
	Teff       2020
	Radius     113255.82
	MassSol    0.01995525
	DiscDate   "2003"
	Orbit
	{
		Period          317
		SemiMajorAxis   19.1089
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "HD 167665 A/HIP 89620 A"
{
	ParentBody "HD 167665"
	Class      "F8 V"
	AppMagn    6.36
	Orbit
	{
		Period          12.01
		SemiMajorAxis   0.217
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "HD 167665 b/2MASS J18172374-2817203 b"           //Unconfirmed
{
	ParentBody "HD 167665"
	MassSol    0.04846275
	DiscDate   "2007"
	Orbit
	{
		Period          12.01
		SemiMajorAxis   5.283
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "WISEPA 18412+70003 A/WISEPA J184124.74+700038.0 A"
{
	ParentBody "WISEPA 18412+70003"
	Class      "T5 V"
	AppMagnJ   17.24
	AppMagnH   17.73
	AppMagnKs  15.63
	AppMagnW1  16.49
	AppMagnW2  14.31
	AppMagnW3  13.05
	MassSol    0.057015
	DiscDate   "2011"
	Orbit
	{
		Period          11
		SemiMajorAxis   1.4
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "WISEPA 18412+70003 B/WISEPA J184124.74+700038.0 B"
{
	ParentBody "WISEPA 18412+70003"
	Class      "T5 V"
	AppMagnJ   17.57
	AppMagnH   17.75
	MassSol    0.057015
	DiscDate   "2011"
	Orbit
	{
		Period          11
		SemiMajorAxis   1.4
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "HD 175679 A/HIP 89620 A"
{
	ParentBody "HD 175679"
	Class      "F8 V"
	AppMagn    6.36
	Orbit
	{
		Period          3.742
		SemiMajorAxis   0.0972
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "HD 175679 b/2MASS J18562560+0228161 b"           //Unconfirmed
{
	ParentBody "HD 175679"
	MassSol    0.03515925
	DiscDate   "2011"
	Orbit
	{
		Period          3.742
		SemiMajorAxis   3.2628
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "DENIS-P 18595-37063 A/DENIS-P J185950.9-370632 A/2MASS J18595094-3706313 A"
{
	ParentBody "DENIS-P 18595-37063"
	Class      "L0 V"
	AppMagnJ   13.98
	AppMagnH   13.1
	AppMagnKs  12.57
	MassSol    0.0171045
	Orbit
	{
		Period          124.6046    //Generic
		SemiMajorAxis   3.4125
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "DENIS-P 18595-37063 B/DENIS-P J185950.9-370632 B/2MASS J18595094-3706313 B"
{
	ParentBody "DENIS-P 18595-37063"
	Class      "L3 V"
	MassSol    0.0133035
	Orbit
	{
		Period          124.6046    //Generic
		SemiMajorAxis   4.3875
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "Wolf 1055 A/V1428 Aql/LHS 473/Ross 652/HIP 94761 A"
{
	ParentBody "Wolf 1055"
	Class      "M3 V"
	AppMagn    9.11
	Radius     375840
	MassSol    0.48
	Orbit
	{
		Period          10701.1607    //Generic
		SemiMajorAxis   54.6887
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Van Biesbroeck Star/Wolf 1055 B/VB 10/V1298 Aql/LHS 474/Gliese 752 B/GJ 752 B/2MASS J19165526+0510086"
{
	ParentBody "Wolf 1055"
	Class      "M8 V"
	Radius     69600
	AppMagn    17.52
	MassSol    0.07602
	Orbit
	{
		Period          10701.1607    //Generic
		SemiMajorAxis   345.3113
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "ETA Tel A/HR 7329 A/CP-54 9339"
{
	ParentBody     "ETA Tel"
	Class          "A0 V"
	AppMagn        5.03
	MassSol        2.2
	Age            0.03

	Orbit
	{
		SemiMajorAxis	2.03	// 136 AU * mass ratio
		Eccentricity    0.47
		Inclination		85		// no data
		AscendingNode	0		// no data
		ArgOfPericenter	180		// no data
		MeanAnomaly		180		// no data; but brown dwarf is at max separation in 2000s
	}
}

Star "ETA Tel B/HR 7329 B/2MASS J19225122-5425263 B"
{
	ParentBody     "ETA Tel"
	Class          "L1 V"	// actually M9.5V, but it will treat as red dwarf in SE
	Luminosity     0.0001
	AppMagn        20
	AppMagnJ       12.06
	AppMagnH       11.75
	AppMagnKs      11.6
	MassSol        0.0333
	Teff           2650
	DiscMethod     "Imaging"
	DiscDate       "2000"
	Orbit
	{
		SemiMajorAxis	133.97	// 136 AU * mass ratio
		Eccentricity    0.47
		Inclination		85		// no data
		AscendingNode	0		// no data
		ArgOfPericenter	0		// no data
		MeanAnomaly		180		// no data; but brown dwarf is at max separation in 2000s
	}
}

Star "HD 182488 A/HIP 95319 A"
{
	ParentBody "HD 182488"
	Class      "G8 V"
	AppMagn    6.37
	Orbit
	{
		Period          297.2725    //Generic
		SemiMajorAxis   0.872
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "HD 182488 B/2MASS J19233402+3313190 B"
{
	ParentBody "HD 182488"
	Class      "T8.5 V"
	AppMagnH   19.26
	Teff       560
	Radius     64318.12
	MassSol    0.019005
	DiscDate   "2009"
	Orbit
	{
		Period          297.2725    //Generic
		SemiMajorAxis   43.128
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "HD 190228 A/HIP 98714 A"
{
	ParentBody "HD 190228"
	Class      "G5 IV"
	AppMagn    7.3
	Orbit
	{
		Period          3.14
		SemiMajorAxis   0.0907
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "HD 190228 b"
{
	ParentBody "HD 190228"
	MassSol    0.04656225
	DiscDate   "2003"
	Orbit
	{
		Period          3.14
		SemiMajorAxis   1.9093
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "15 Sge A/HR 7672/HD 190406 A/HIP 98819 A"
{
	ParentBody "15 Sge"
	Class      "G1 V"
	AppMagn    5.8
	Orbit
	{
		Period          73.3
		SemiMajorAxis   1.0383
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "HD 190406 B/2MASS J20035517+1705040 B"
{
	ParentBody "15 Sge"
	Class      "L4.5 V"
	AppMagnJ   14.39
	AppMagnH   14.04
	AppMagnKs  13.04
	Teff       1680
	Radius     69911
	MassSol    0.06556725
	DiscDate   "1999"
	Orbit
	{
		Period          73.3
		SemiMajorAxis   17.2617
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "HD 189310 A/HIP 99634 A"
{
	ParentBody "HD 189310"
	Class      "K2 V"
	AppMagn    8.46
	Orbit
	{
		Period          0.03884
		SemiMajorAxis   0.0031
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "HD 189310 b/2MASS J20131646-8201413 b"           //Unconfirmed
{
	ParentBody "HD 189310"
	MassSol    0.0247065
	DiscDate   "2010"
	Orbit
	{
		Period          0.03884
		SemiMajorAxis   0.1059
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "2MASS 20261-29431 A/2MASS J20261584-2943124 A"
{
	ParentBody "2MASS 20261-29431"
	Class      "L0.5 V"
	AppMagnJ   14.86
	AppMagnH   13.97
	AppMagnKs  13.36
	AppMagnK   13.37
	MassSol    0.07697025
	DiscDate   "1999"
	Orbit
	{
		Period          82.1848    //Generic
		SemiMajorAxis   2.5487
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "2MASS 20261-29431 B/2MASS J20261584-2943124 B"
{
	ParentBody "2MASS 20261-29431"
	Class      "T6 V"
	AppMagnJ   17.98
	AppMagnH   18.2
	AppMagnK   18.1
	MassSol    0.030408
	Orbit
	{
		Period          82.1848    //Generic
		SemiMajorAxis   6.4513
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "2MASS 21011+17565 A/2MASS J21011544+1756586 A"
{
	ParentBody "2MASS 21011+17565"
	Class      "L7 V"
	AppMagnR   23.25
	AppMagnI   20.73
	AppMagnJ   16.85
	AppMagnH   15.86
	AppMagnKs  14.89
	AppMagnK   16.92
	AppMagnW2  13.51
	MassSol    0.06746775
	DiscDate   "2000"
	Orbit
	{
		Period          84
		SemiMajorAxis   3.8158
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "2MASS 21011+17565 B/2MASS J21011544+1756586 B"
{
	ParentBody "2MASS 21011+17565"
	Class      "L8 V"
	MassSol    0.064617
	DiscDate   "2003"
	Orbit
	{
		Period          84
		SemiMajorAxis   3.9842
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "2MASS 21321+13415 A/2MASS J21321145+1341584 A"
{
	ParentBody "2MASS 21321+13415"
	Class      "L5 V"
	AppMagnJ   16.2
	AppMagnH   14.99
	AppMagnKs  14.26
	MassSol    0.0779205
	DiscDate   "2002"
	Orbit
	{
		Period          10
		SemiMajorAxis   0.8832
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "2MASS 21321+13415 B/2MASS J21321145+1341584 B"           //Unconfirmed
{
	ParentBody "2MASS 21321+13415"
	Class      "L7.5 V"
	AppMagnJ   17.05
	AppMagnH   15.9
	AppMagnKs  15.08
	MassSol    0.07506975
	Orbit
	{
		Period          10
		SemiMajorAxis   0.9168
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "CLO 2 A/2MASS J21402931+1625183/ALP 26 A/SDSS J214029.28+162517.5/WDS J21405+1625 A"
{
	ParentBody "CLO 2"
	Class      "M8.5 V"
	Orbit
	{
		Period          20.1
		SemiMajorAxis   1.3244
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "2MASS 21402+16251 B/2MASS J21402931+1625183 B"
{
	ParentBody "CLO 2"
	Class      "L0 V"
	AppMagnI   16.4
	AppMagnJ   14.15
	AppMagnH   13.44
	AppMagnKs  12.97
	AppMagnK   12.83
	Teff       2075
	Radius     64318.12
	MassSol    0.0475125
	DiscDate   "2003"
	Orbit
	{
		Period          20.1
		SemiMajorAxis   2.2856
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "Wolf 940 A/LHS 3408/G 93-25/2MASS J21464040-0010233 A"
{
	ParentBody "Wolf 940"
	Class      "M4 V"
	AppMagn    12.7
	Orbit
	{
		Period          15036.3299    //Generic
		SemiMajorAxis   48.1978
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Wolf 940 B/2MASS J21464040-0010233 B"
{
	ParentBody "Wolf 940"
	Class      "T8.5 V"
	AppMagnJ   18.16
	AppMagnH   18.77
	AppMagnKs  18.85
	AppMagnK   18.97
	AppMagnW1  16.72
	AppMagnW2  14.24
	Teff       605
	Radius     61521.68
	MassSol    0.03325875
	DiscDate   "2009"
	Orbit
	{
		Period          15036.3299    //Generic
		SemiMajorAxis   347.8022
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "2MASS 21474+14313 A/2MASS J21474365+1431315 A"           //Unconfirmed
{
	ParentBody "2MASS 21474+14313"
	Class      "L0 V"
	AppMagnI   17.3
	AppMagnJ   13.82
	AppMagnH   13.11
	AppMagnKs  12.65
	MassSol    0.083622
	DiscDate   "2003"
	Orbit
	{
		Period          65
		SemiMajorAxis   3.3765
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "2MASS 21474+14313 B/2MASS J21474365+1431315 B"           //Unconfirmed
{
	ParentBody "2MASS 21474+14313"
	Class      "L2 V"
	MassSol    0.0779205
	DiscDate   "2003"
	Orbit
	{
		Period          65
		SemiMajorAxis   3.6235
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "2MASS 21522+09375 A/2MASS J21522609+0937575 A"
{
	ParentBody "2MASS 21522+09375"
	Class      "L6 V"
	AppMagnJ   15.19
	AppMagnH   14.08
	AppMagnKs  13.34
	MassSol    0.07506975
	DiscDate   "2006"
	Orbit
	{
		Period          56
		SemiMajorAxis   3
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "2MASS 21522+09375 B/2MASS J21522609+0937575 B"
{
	ParentBody "2MASS 21522+09375"
	Class      "L6 V"
	MassSol    0.07506975
	DiscDate   "2008"
	Orbit
	{
		Period          56
		SemiMajorAxis   3
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "EPS Ind A/HD 198726"
{
	ParentBody "EPS Ind"
	Class      "K4 V"
	AppMagn    6.89
	MassSol    0.78
	Orbit
	{
		Period          217.475218
		SemiMajorAxis   125.580934
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Barycenter "EPS Ind B"
{
	ParentBody "EPS Ind"
	Orbit
	{
		Period          217.475218
		SemiMajorAxis   1374.419066
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "EPS Ind Ba/2MASS J22041052-5646577 A"
{
	ParentBody "EPS Ind B"
	Class      "T1 V"
	AppMagnR   20.75
	AppMagnI   16.59
	AppMagnJ   12.29
	AppMagnH   11.51
	AppMagnKs  11.35
	AppMagnK   11.17
	AppMagnW1  10.63
	AppMagnW2  9.44
	AppMagnW3  8.35
	Teff       1276
	Radius     62220.79
	MassSol    0.04466175
	DiscDate   "2003"
	Orbit
	{
		Period          22
		SemiMajorAxis   0.9893
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "EPS Ind Bb/2MASS J22041052-5646577 B"
{
	ParentBody "EPS Ind B"
	Class      "T6 V"
	AppMagnJ   13.23
	AppMagnH   13.27
	AppMagnKs  13.53
	Teff       854
	Radius     65716.34
	MassSol    0.026607
	DiscDate   "2003"
	Orbit
	{
		Period          22
		SemiMajorAxis   1.6607
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "SDSSp 22495+00440 A/SDSSp J224953.45+004404.2 A/2MASS J22495345+0044046 A"
{
	ParentBody "SDSSp 22495+00440"
	Class      "L3 V"
	AppMagnr   23.98
	AppMagni   21.64
	AppMagnJ   16.83
	AppMagnH   15.74
	AppMagnKs  14.36
	AppMagnK   14.82
	AppMagnW1  13.58
	AppMagnW2  13.14
	AppMagnW3  11.28
	MassSol    0.0285075
	DiscDate   "2002"
	Orbit
	{
		Period          250
		SemiMajorAxis   6.5094
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "SDSSp 22495+00440 B/SDSSp J224953.45+004404.2 B/2MASS J22495345+0044046 B"
{
	ParentBody "SDSSp 22495+00440"
	Class      "L5 V"
	AppMagnJ   17.85
	AppMagnH   16.69
	AppMagnK   15.71
	MassSol    0.02185575
	DiscDate   "2009"
	Orbit
	{
		Period          250
		SemiMajorAxis   8.4906
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "DENIS-P 22521-17301 A/DENIS-P J225210.73-173013.4 A/2MASS J22521073-1730134 A"
{
	ParentBody "DENIS-P 22521-17301"
	Class      "L4.5 V"
	AppMagnI   17.9
	AppMagnJ   14.31
	AppMagnH   13.36
	AppMagnKs  12.9
	AppMagnK   13
	MassSol    0.07506975
	DiscDate   "2003"
	Orbit
	{
		Period          10
		SemiMajorAxis   0.8789
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "DENIS-P 22521-17301 B/DENIS-P J225210.73-173013.4 B/2MASS J22521073-1730134 B"
{
	ParentBody "DENIS-P 22521-17301"
	Class      "T3.5 V"
	AppMagnK   14.72
	MassSol    0.064617
	Orbit
	{
		Period          10
		SemiMajorAxis   1.0211
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "2MASS 22551-57130 A/2MASS J22551861-5713056 A"
{
	ParentBody "2MASS 22551-57130"
	Class      "L6 V"
	AppMagnJ   14.08
	AppMagnH   13.19
	AppMagnKs  12.58
	MassSol    0.07506975
	DiscDate   "2002"
	Orbit
	{
		Period          7.5
		SemiMajorAxis   0.7739
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "2MASS 22551-57130 B/2MASS J22551861-5713056 B"
{
	ParentBody "2MASS 22551-57130"
	Class      "L8 V"
	MassSol    0.0703185
	Orbit
	{
		Period          7.5
		SemiMajorAxis   0.8261
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "HD 217165 A/HIP 113438 A"
{
	ParentBody "HD 217165"
	Class      "G0 V"
	AppMagn    7.67
	Orbit
	{
		Period          11.1
		SemiMajorAxis   0.1949
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "HD 217165 b/2MASS J22582988+0949319 b"           //Unconfirmed
{
	ParentBody "HD 217165"
	MassSol    0.0437115
	DiscDate   "2007"
	Orbit
	{
		Period          11.1
		SemiMajorAxis   4.9051
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "SDSS 23355-00130 A/SDSS J2335583-001304 A/2MASS J23355849-0013042 A"           //Unconfirmed
{
	ParentBody "SDSS 23355-00130"
	Class      "L1 V"
	AppMagni   19.35
	AppMagnI   18.9
	AppMagnJ   15.98
	AppMagnH   15.2
	AppMagnK   14.71
	MassSol    0.07887075
	DiscDate   "2003"
	Orbit
	{
		Period          24
		SemiMajorAxis   1.6957
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "SDSS 23355-00130 B/SDSS J2335583-001304 B/2MASS J23355849-0013042 B"
{
	ParentBody "SDSS 23355-00130"
	Class      "L4 V"
	MassSol    0.0741195
	DiscDate   "2003"
	Orbit
	{
		Period          24
		SemiMajorAxis   1.8043
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "1RXS 23513+31272 A"
{
	ParentBody "1RXS 23513+31272"
	Class      "M2 V"
	Orbit
	{
		Period          1777.8762    //Generic
		SemiMajorAxis   6.8222
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "1RXS 23513+31272 B/1RXS J235133.3+312720 B/2MASS J23513366+3127229 B"
{
	ParentBody "1RXS 23513+31272"
	Class      "L0 V"
	AppMagnH   14.89
	AppMagnK   13.92
	MassSol    0.030408
	DiscDate   "2012"
	Orbit
	{
		Period          1777.8762    //Generic
		SemiMajorAxis   112.1778
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "WASP-30 A/TYC 5834-95-1/UCAC3 160-301145"
{
	ParentBody "WASP-30"
	Class      "F8 V"
	AppMagn    11.91
	Orbit
	{
		Period          0.001138
		SemiMajorAxis   0.0027
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "WASP-30 b/2MASS J23533805-1007049 b"
{
	ParentBody "WASP-30"
	Radius     66415.45
	MassSol    0.05986575
	DiscDate   "2013"
	Orbit
	{
		Period          0.001138
		SemiMajorAxis   0.0526
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "WISE 00135-18164 A/WISE J001358.81-181648.1 A/2MASS J00135882-1816462 A A"
{
	ParentBody "WISE 00135-18164"
	Class      "L5 V"
	AppMagnJ   16.68
	AppMagnH   15.87
	AppMagnKs  15.04
	AppMagnK   15.18
	AppMagnW1  14.6
	AppMagnW2  14.17
	AppMagnW3  12.2
	MassSol    0.07506975
	DiscDate   "2015"
	Orbit
	{
		Period          900000
		SemiMajorAxis   2487.395
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "WISE 00135-18164 B/WISE J001358.81-181648.1 B/2MASS J00135882-1816462 B"           //Unconfirmed
{
	ParentBody "WISE 00135-18164"
	MassSol    0.03801				//Generic
	DiscDate   "2015"
	Orbit
	{
		Period          900000
		SemiMajorAxis   4912.605
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "HD 14348 A/HIP 10868 A"
{
	ParentBody "HD 14348"
	Class      "F5 V"
	AppMagn    7.19
	Orbit
	{
		Period          12.987
		SemiMajorAxis   0.2027
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "HD 14348 B/2MASS J02195291+3120150 B"           //Unconfirmed
{
	ParentBody "HD 14348"
	MassSol    0.04656225
	DiscDate   "2015"
	Orbit
	{
		Period          12.987
		SemiMajorAxis   5.7473
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "HD 18757 A/HIP 14286 A"
{
	ParentBody "HD 18757"
	Class      "G4 V"
	AppMagn    6.64
	Orbit
	{
		Period          109
		SemiMajorAxis   0.7216
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "HD 18757 B/2MASS J03040962+6142212 B"           //Unconfirmed
{
	ParentBody "HD 18757"
	MassSol    0.03325875
	DiscDate   "2015"
	Orbit
	{
		Period          109
		SemiMajorAxis   21.4784
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "HD 72946 A/HIP 42173 A"
{
	ParentBody "HD 72946"
	Class      "G5 V"
	AppMagn    7.25
	Orbit
	{
		Period          15.93
		SemiMajorAxis   0.3502
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "HD 72946 B/2MASS J08355126+0637218 B"           //Unconfirmed
{
	ParentBody "HD 72946"
	MassSol    0.057015
	DiscDate   "2015"
	Orbit
	{
		Period          15.93
		SemiMajorAxis   6.0198
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "HD 76346 A/HIP 43620 A"
{
	ParentBody "HD 76346"
	Class      "A0 V"
	AppMagn    6.02
	Orbit
	{
		Period          466.0076    //Generic
		SemiMajorAxis   1.5557
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "HD 76346 B"
{
	ParentBody "HD 76346"
	Teff       2300
	MassSol    0.045612
	DiscDate   "2015"
	Orbit
	{
		Period          466.0076    //Generic
		SemiMajorAxis   78.4443
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "HIP 65423 A/HD 116402 A"
{
	ParentBody "HIP 65423"
	Class      "G5 V"
	AbsMagn    4.67
	Orbit
	{
		Period          3379.8158    //Generic
		SemiMajorAxis   11.5437
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "HIP 65423 B"
{
	ParentBody "HIP 65423"
	AppMagnH   12.93
	AppMagnK   12.51
	Teff       2770
	MassSol    0.05226375
	Orbit
	{
		Period          3379.8158    //Generic
		SemiMajorAxis   216.4563
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "HIP 65517 A/HD 116650 A"
{
	ParentBody "HIP 65517"
	Class      "K0 V"
	AppMagn    9.76
	Orbit
	{
		Period          249.2596    //Generic
		SemiMajorAxis   2.458
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "HIP 65517 B"
{
	ParentBody "HIP 65517"
	AppMagnH   12.85
	AppMagnK   12.54
	Teff       2960
	MassSol    0.05986575
	Orbit
	{
		Period          249.2596    //Generic
		SemiMajorAxis   36.542
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "HIP 72099 A/HD 129683 A"
{
	ParentBody "HIP 72099"
	Class      "F6 V"
	AppMagn    9.66
	Orbit
	{
		Period          962.1626    //Generic
		SemiMajorAxis   5.4062
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "HIP 72099 B"           //Unconfirmed
{
	ParentBody "HIP 72099"
	AppMagnH   12.89
	AppMagnK   12.57
	Teff       2770
	MassSol    0.0665175
	Orbit
	{
		Period          962.1626    //Generic
		SemiMajorAxis   101.5938
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "HD 209262 A/HIP 108761 A"
{
	ParentBody "HD 209262"
	Class      "G5 V"
	AppMagn    8
	Orbit
	{
		Period          14.88
		SemiMajorAxis   0.1854
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "HD 209262 B/2MASS J22015413+0446136 B"           //Unconfirmed
{
	ParentBody "HD 209262"
	MassSol    0.030408
	DiscDate   "2015"
	Orbit
	{
		Period          14.88
		SemiMajorAxis   5.9746
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//G 216-7;6thCVB, SIMBAD

Barycenter "G 216-7 (AB)"
{
	ParentBody "G 216-7"
	Orbit
	{
		Period          14974.7321    //Generic
		SemiMajorAxis   39.3936
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star   "G 216-7 A/G 189-30 A/LTT 16634 A/NLTT 54384 A/BD+38 4818 A/SAO 72548/HIP 111685"
{
	ParentBody "G 216-7 (AB)"
	Class 	   "K7 V"
	AppMagn    10.04
	Orbit
	{
		Period 			16.77
		SemiMajorAxis 	2.4801
		Eccentricity 	0.256
		Inclination 	55.9
		AscendingNode 	69
		ArgOfPericenter 118.7
		Epoch 			2448542.242524
		MeanAnomaly 	0
	}
}

Star   "G 216-7 B/G 189-30 B/LTT 16634 B/NLTT 54384 B/BD+38 4818 B"
{
	ParentBody "G 216-7 (AB)"
	Class "M3 V"
	AppMagn 10.47
	Orbit
	{
		Period 			16.77
		SemiMajorAxis 	3.9681
		Eccentricity 	0.256
		Inclination 	55.9
		AscendingNode 	69
		ArgOfPericenter 298.7
		Epoch 			2448542.242524
		MeanAnomaly 	0
	}
}

Star "G 216-7 C/2MASS J22373255+3922398 C"
{
	ParentBody "G 216-7"
	Class      "M9.5 V"
	AppMagnR   19.2
	AppMagnI   16.1
	AppMagnJ   13.34
	AppMagnH   12.69
	AppMagnKs  12.18
	AppMagnK   12.18
	MassSol    0.06936825
	DiscDate   "2001"
	Orbit
	{
		Period          14974.7321    //Generic
		SemiMajorAxis   590.6064
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//ETA Cnc;english, spanish wiki

Star "ETA Cnc A/33 Cnc A/HR 3366/HD 72292 A/GC 11687/HIP 41909 A"
{
	ParentBody "ETA Cnc"
	Class      "K3 III"
	AppMagn    5.34
	MassSol    1.6
	Teff	   4345
	Orbit
	{
		Period          1425124.872    //Generic
		SemiMajorAxis   600.3076
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "ETA Cnc B/2MASS J08324250+2026281 B"
{
	ParentBody "ETA Cnc"
	Class      "L0.5 V"
	AppMagnJ   17.78
	Teff       1920
	MassSol    0.0665175
	Orbit
	{
		Period          1425124.872    //Generic
		SemiMajorAxis   14439.6924
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//Alula Australis;6thCVB, english and spanish wiki

Barycenter "Alula Australis (AB)"
{
	ParentBody "Alula Australis"
	Orbit
	{
		Period          156465.1213    //Generic
		SemiMajorAxis   62.8419
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Barycenter "Alula Australis A"
{
	ParentBody "Alula Australis (AB)"
	Orbit
	{
		Period          59.878
		SemiMajorAxis   9.34104678
		Eccentricity    0.398
		Inclination     122.13
		AscendingNode   101.85
		ArgOfPericenter 127.94
		Epoch           2427875.012706
		MeanAnomaly     0
	}
}

Barycenter "Alula Australis B"
{
	ParentBody "Alula Australis (AB)"
	Orbit
	{
		Period          59.878
		SemiMajorAxis   13.21846242
		Eccentricity    0.398
		Inclination     122.13
		AscendingNode   101.85
		ArgOfPericenter 307.94
		Epoch           2427875.012706
		MeanAnomaly     0
	}
}

Star "Alula Australis Aa/HIP 55203/HD 9830/HR 4374"
{
	ParentBody "Alula Australis A"
	Class      "G0 V"
	Radius     723840
	AppMagn    4.33
	MassSol    1
	Orbit
	{
		Period          1.834
		SemiMajorAxis   0.1601227
		Eccentricity    0.61
		Inclination     91
		AscendingNode   318
		ArgOfPericenter 324
		Epoch           2448728.516046
		MeanAnomaly     0
	}
}

Star "Alula Australis Ab"
{
	ParentBody "Alula Australis A"
	Class      "M3 V"
	MassSol    0.5
	Orbit
	{
		Period          1.834
		SemiMajorAxis   0.3202454
		Eccentricity    0.61
		Inclination     91
		AscendingNode   318
		ArgOfPericenter 144
		Epoch           2448728.516046
		MeanAnomaly     0
	}
}

Star "Alula Australis Ba/HD 98231/HR 4375"
{
	ParentBody "Alula Australis B"
	Class      "G5 V"
	Radius     626400
	AppMagn    4.8
	MassSol    0.98
	Orbit
	{
		Period          0.0109
		SemiMajorAxis   0.0045283
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Alula Australis Bb"
{
	ParentBody "Alula Australis B"
	MassSol    0.08
	Orbit
	{
		Period          0.0109
		SemiMajorAxis   0.0554717
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "Alula Australis E/KSI UMa E/2MASS J11183876+3125441"
{
	ParentBody "Alula Australis"
	Class      "T8.5 V"
	AppMagnJ   17.8
	AppMagnH   17.92
	AppMagnKs  18.75
	AppMagnW1  16.16
	AppMagnW2  13.31
	AppMagnW3  12.36
	Teff       675
	MassSol    0.04086075
	DiscDate   "2012"
	Orbit
	{
		Period          156465.1213    //Generic
		SemiMajorAxis   3937.1581
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "Wolf 1130 A/GJ 781/G 230-26/Ross 1069b/LHS 482/HIP 98906 A"
{
	ParentBody "Wolf 1130"
	Class      "M1.5 VI"
	AppMagn    11.97
	Orbit
	{
		Period          200000
		SemiMajorAxis   189.9955
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Wolf 1130 B"
{
	ParentBody "Wolf 1130"
	Class      "T8 V"
	AppMagnJ   19.64
	AppMagnH   19.57
	AppMagnW1  18.82
	AppMagnW2  14.94
	AppMagnW3  12.97
	Teff       750
	Radius     63619.01
	MassSol    0.03515925
	DiscDate   "2013"
	Orbit
	{
		Period          200000
		SemiMajorAxis   2810.0045
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

Star "Wolf 1084 A/GJ 802/LHS 498/G 231-13"
{
	ParentBody "Wolf 1084"
	Class      "M5 V"
	AppMagn    14.67
	Orbit
	{
		Period          3.8152    //Generic
		SemiMajorAxis   0.4305
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "Wolf 1084 B/2MASS J20431920+5520521 B"
{
	ParentBody "Wolf 1084"
	Class      "L6 V"
	MassSol    0.0627165
	DiscDate   "2008"
	Orbit
	{
		Period          3.8152    //Generic
		SemiMajorAxis   1.0295
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//LHS 6343;SIMBAD,exoplanet.eu

Star "LHS 6343 AC/G 205-57/KOI-959/NTLL 47499"
{
	ParentBody "LHS 6343"
	Class      "M V"
	AppMagn    13.88
	MassSol    0.37
	Teff	   3130
	Radius	   263088
	Orbit
	{
		Period          0.035
		SemiMajorAxis   0.013
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "LHS 6343 b/2MASS J19101435+4657261 b"
{
	ParentBody "LHS 6343"
	Radius     60123.46
	MassSol    0.0665175
	Orbit
	{
		Period          0.035
		SemiMajorAxis   0.072
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}

//GG Tau
//Article:
//TEST OF PREMAIN-SEQUENCE EVOLUTIONARY MODELS ACROSS THE STELLAR/SUBSTELLAR
//BOUNDARY BASED ON SPECTRA OF THE YOUNG QUADRUPLE GG TAURI
//Authors: RUSSEL J. WHITE, A. M. GHEZ, I. NEILL REID AND GREG SCHULTZ

Barycenter "GG Tau A"
{
	ParentBody "GG Tau"
	Orbit
	{
		Period 			41811.79
		SemiMajorAxis 	139.58
		ArgOfPericenter 0
		MeanAnomaly 	0
	}
}

Barycenter "GG Tau B"
{
	ParentBody "GG Tau"
	Orbit
	{
		Period 			41811.79
		SemiMajorAxis 	1274.42
		ArgOfPericenter 180
		MeanAnomaly 	0
	}
}

Star "GG Tau Aa"
{
	ParentBody "GG Tau A"
	Class 	   "K7 V"
	MassSol	   0.78
	AppMagn    12.3
	Orbit
	{
		Period 			171.51
		SemiMajorAxis 	16.3
		ArgOfPericenter 0
		MeanAnomaly 	0
	}
}

Star "GG Tau Ab"
{
	ParentBody "GG Tau A"
	Class 	   "M0 V"
	MassSol    0.68
	AppMagn    15.22
	Orbit
	{
		Period 			171.51
		SemiMajorAxis 	18.7
		ArgOfPericenter 180
		MeanAnomaly 	0
	}
}

Star "GG Tau Ba"
{
	ParentBody "GG Tau B"
	Class      "M5 V"
	AppMagn    16.97
	MassSol    0.12
	Orbit
	{
		Period 			7453.96
		SemiMajorAxis 	51.66
		ArgOfPericenter 0
		MeanAnomaly 	0
	}
}

Star "GG Tau Bb"
{
	ParentBody "GG Tau B"
	Class 		"M7 V"
	MassSol     0.0399
	DiscDate    "1999"
	Orbit
	{
		Period 			7453.96
		SemiMajorAxis 	155.34
		ArgOfPericenter 180
		MeanAnomaly 	0
	}
}

/////////////BROWN BINARIES WITH UNKOWN DISTANCE DATA////////////////////

Star "V2505 Oph A/BD-21 4369/TYC 6215-184-1/ScoPMS 214"
{
	ParentBody "V2505 Oph"
	Class      "K2 IV"
	AppMagn    11.315
	Orbit
	{
		Period          10186.142    //Generic
		SemiMajorAxis   12.2349
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star "V2505 Oph B/2MASS J16294869-2152118 B"
{
	ParentBody "V2505 Oph"
	Class      "M6 V"
	MassSol    0.02375625
	DiscDate   "2006"
	Orbit
	{
		Period          10186.142    //Generic
		SemiMajorAxis   437.7651
		ArgOfPericenter 180
		MeanAnomaly     0
	}
}


Star	"Gliese 570 A" // should also have designations "HD 131977/HIP 73184" instead of the barycenter
{
	ParentBody  "Gliese 570"
	Class       "K4V"
	AppMagn     5.64
	Lum         0.156
	MassSol     0.76
	Radius      535920
	Temperature 4170
	FeH         0.0086

	RotationPeriod 1159.2

	Orbit
	{
		Epoch           1689
		Period          2130
		SemiMajorAxis   86.99 // mass ratio * 190
		Eccentricity    0.2
		Inclination     72.53
		AscendingNode   317.31
		LongOfPericen   212
		MeanAnomaly     0
	}
}

Barycenter	"Gliese 570 (BC)/HD 131976/HIP 73182"
{
	ParentBody  "Gliese 570"
	MassSol     0.9

	Orbit
	{
		Epoch           1689
		Period          2130
		SemiMajorAxis   103.01 // mass ratio * 190
		Eccentricity    0.2
		Inclination     72.53
		AscendingNode   317.31
		LongOfPericen   32
		MeanAnomaly     0
	}
}

Star	"Gliese 570 B"
{
	ParentBody  "Gliese 570 (BC)"
	Class       "M1V"
	AppMagn     9.9
	Lum         0.019
	MassSol     0.55
	Radius      452400
	Temperature 2700

	Orbit
	{
		Epoch           1996.51105
		Period          0.845696
		SemiMajorAxis   0.307 // mass ratio * 0.79
		Eccentricity    0.7559
		Inclination     107.6
		AscendingNode   195.9
		LongOfPericen   252.1
		MeanAnomaly     0
	}
}

Star	"Gliese 570 C"
{
	ParentBody  "Gliese 570 (BC)"
	Class       "M3V"
	Lum         0.003
	MassSol     0.35

	Orbit
	{
		Epoch           1996.51105
		Period          0.845696
		SemiMajorAxis   0.483 // mass ratio * 0.79
		Eccentricity    0.7559
		Inclination     107.6
		AscendingNode   195.9
		LongOfPericen   72.1
		MeanAnomaly     0
	}
}

Star	"Gliese 570 D"
{
	ParentBody  "Gliese 570"
	Class       "T7V"
	MassSol     0.05
	Temperature 500

	Orbit
	{
		SemiMajorAxis   1500
		Inclination     72.53  // used the same as for A + BC pair
		AscendingNode   317.31 // used the same as for A + BC pair
	}
}

Star	"Oph 1622 A"
{
	ParentBody "Oph 162225-240515"
	Class      "L1V"
	MassSol     0.016
	Orbit
	{
		Period         11.8622
		SemiMajorAxis  112
		Eccentricity   0
		Inclination    124
		AscendingNode  100.556
		LongOfPericen  88.7741
		MeanLongitude  34.404
	}
}

Star	"Oph 1622 B"
{
	ParentBody "Oph 162225-240515"
	Class      "L3V"
	MassSol     0.014
	Orbit
	{
		Period         11.8622
		SemiMajorAxis  128
		Eccentricity   0
		Inclination    124
		AscendingNode  100.556
		ArgOfPericen   268.7741
		MeanLongitude  214.404
	}
}

Star "SCR 1845-6357 A"
{
	ParentBody "SCR 1845-6357"
	Class      "M8.5 V"
	AppMagn		17.4
	MassSol     0.07
	Radius      66700
	Orbit
	{
		SemiMajorAxis	1.6
		Period			12.84
		ArgOfPericenter	0
		MeanAnomaly		0
	}
}

Star "SCR 1845-6357 B"
{
	ParentBody     "SCR 1845-6357"
	Class	       "T6 V"
	MassSol         0.045
	Orbit
	{
		SemiMajorAxis	2.5
		Period			12.84
		ArgOfPericenter	180
		MeanAnomaly		0
	}
}

Star	"Luhman 16 A/LUH 16 A/Luhman-Wise 1 A/WISE 1049 A/WISE J104915.57-531906.1 A/IRAS Z10473-5303 A/AKARI J1049166-531907 A"
{
	ParentBody  "Luhman 16"
	Class       "L8V"
	AppMag      25
	Teff        1350
	Orbit
	{
		Period          25
		SemiMajorAxis   1.35
		ArgOfPericenter 0
		MeanAnomaly     0
	}
}

Star	"Luhman 16 B/LUH 16 B/Luhman-Wise 1 B/WISE 1049 B/WISE J104915.57-531906.1 B/IRAS Z10473-5303 B/AKARI J1049166-531907 B"
{
	ParentBody  "Luhman 16"
	Class       "T1V"
	AppMag      25
	Teff        1210
	Orbit
	{
		Period          25
		SemiMajorAxis   1.65
		ArgOfPericenter 180
		MeanAnomaly     0		  
	}
}

// Moved from Exoplanets and ExoplanetsSuns catalogs
Star	"WISE 0458+6434 A"
{
	ParentBody  "WISE 0458+6434"
	Class       "T8.5V"
	AppMagn      17.41	// H band
	MassSol      0.014
	Teff         600
	FeH          0
	Age          1

	Orbit
	{
		Period          70.63806076
		SemiMajorAxis   2.02	// mass ratio * 5
		ArgOfPericenter 180
		MeanAnomaly     0		  
	}
}

Star	"WISE 0458+6434 B"
{
	ParentBody  "WISE 0458+6434"
	Class       "T9.5V"
	AppMagn      18.79	// H band
	MassSol      0.0095
	Teff         500
	DiscMethod  "Imaging"
	DiscDate    "2011"

	Orbit
	{
		Period          70.63806076
		SemiMajorAxis   2.98	// mass ratio * 5
		ArgOfPericenter 0
		MeanAnomaly     0		  
	}
}

// moved from Exoplanets and ExoplanetsStars catalogs
Star	"WISE 1217+1626 A"
{
	ParentBody  "WISE 1217+1626"
	Class       "T8.5V"
	AppMagn      18.94	// K band
	Lum          1.122e-6
	MassSol      0.0276
	Radius       63336
	Teff         575
	Age          6

	Orbit
	{
		Period          129.7769023
		SemiMajorAxis   2.95	// mass ratio * 7.6
		ArgOfPericenter 180
		MeanAnomaly     0		  
	}
}

Star	"WISE 1217+1626 B"
{
	ParentBody  "WISE 1217+1626"
	Class       "L9.9V"	// Y0V
	AppMagn      21.1	// K band
	Lum          1.622e-7
	MassSol      0.0175
	Radius       68632.32
	Teff         400
	DiscMethod  "Imaging"
	DiscDate    "2012"

	Orbit
	{
		Period          129.7769023
		SemiMajorAxis   4.65	// mass ratio * 7.6
		ArgOfPericenter 0
		MeanAnomaly     0		  
	}
}

Star	"CFBDSIR J145829+101343 A"
{
	ParentBody  "CFBDSIR J145829+101343"
	Class       "T9.5V"
	AppMagn      20.18	// H band
	Lum          1.9e-6
	MassSol      0.02
	Teff         580.5
	Age          3

	Orbit
	{
		Period          27.48176492
		SemiMajorAxis   0.866	// mass ratio * 2.6
		ArgOfPericenter 180		// random
		MeanAnomaly     0		  
	}
}

Star	"CFBDSIR J145829+101343 B"
{
	ParentBody  "CFBDSIR J145829+101343"
	Class       "T9.9V"	// Y0V
	AppMagn      22.51	// H band
	Lum          2.95e-7
	MassSol      0.01
	Teff         370
	DiscMethod  "Imaging"
	DiscDate    "2011"

	Orbit
	{
		Period          27.48176492
		SemiMajorAxis   1.733	// mass ratio * 2.6
		ArgOfPericenter 0		// random
		MeanAnomaly     0		  
	}
}

Star	"Scholz/WISE J0720-0846 A"
{
	ParentBody  "WISE J0720-0846"
	Class       "M9V"
	MassSol      0.081
	Age          5.0
	Orbit
	{
		Period          3.800218153
		SemiMajorAxis   0.55	// mas ratio * 1.3
		Eccentricity    0.8
		Inclination     93.5
		ArgOfPericen    234
		MeanAnomaly     0		  
	}
}

Star	"WISE J0720-0846 B"
{
	ParentBody   "WISE J0720-0846"
	Class        "T5V"
	MassSol       0.059
	DiscMethod   "Imaging"
	DiscDate     "2014"
	Orbit
	{
		Period          3.800218153
		SemiMajorAxis   0.75 // mas ratio * 1.3
		Eccentricity    0.8
		Inclination     93.5
		ArgOfPericen    54
		MeanAnomaly     0		  
	}
}

// Data from: arXiv:1309.1073v1

Barycenter	"TWA 5 A"
{
	ParentBody	"TWA 5"
	MassSol		0.9

	Orbit
	{
		Epoch           2513018
		Period          1380
		SemiMajorAxis	3.3	// mass ratio 0.9:0.024 and a = 127
		Eccentricity	0.24
		Inclination		138
		AscendingNode	36.1
		ArgOfPericenter	292
		MeanAnomaly		0
	}
}

Star	"TWA 5 B"
{
	ParentBody     "TWA 5"
	Class          "L5"
	Mass            7955	// 0.024 Msol
	DiscMethod     "Imaging"
	DiscDate       "2009"

	Orbit
	{
		Epoch           2513018
		Period          1380
		SemiMajorAxis	123.7	// mass ratio 0.9:0.024 and a = 127
		Eccentricity	0.24
		Inclination		138
		AscendingNode	36.1
		ArgOfPericenter	112
		MeanAnomaly		0
	}
}

Star	"TWA 5 Aa"
{
	ParentBody	"TWA 5 A"
	MassSol		0.51

	Orbit
	{
		Epoch           2455328
		Period          6.025
		SemiMajorAxis	1.387	// mass ratio 1.3 and a = 3.2
		Eccentricity    0.775
		Inclination		97.5
		AscendingNode	36.5
		ArgOfPericenter	73.1
		MeanAnomaly		0
	}
}

Star	"TWA 5 Ab"
{
	ParentBody	"TWA 5 A"
	MassSol		0.39

	Orbit
	{
		Epoch           2455328
		Period          6.025
		SemiMajorAxis	1.813		// mass ratio 1.3 and a = 3.2
		Eccentricity    0.775
		Inclination		97.5
		AscendingNode	36.5
		ArgOfPericenter	253.1
		MeanAnomaly		0
	}
}
