// Star solver log level:
// 0 - do not log
// 1 - log errors and warnings only
// 2 - log everything
LogLevel    1

///////////////////////////////////////////////////////////
//                       The Sun                         //
///////////////////////////////////////////////////////////

StarBarycenter	"Solar System"
{
	RA      0
	Dec     0
	Dist    0
	Lum     1
	Class  "G2V"
	MassSol 1
	RadSol  1
	Teff    5778
	Age     4.57
	FeH     0
}

///////////////////////////////////////////////////////////
//            Some famous supergiant stars               //
///////////////////////////////////////////////////////////

Star	"VY CMa/HD 58061/HIP 35793"
{
	RA       07 22 58.3315
	Dec     -25 46 03.174
	Dist     1500
	Class   "M4 II"
	AppMagn  7.9607
	Radius   1356225000
	MassSol  20
}

Star	"R136a1"
{
	RA       05 38 42.43
	Dec     -69 06 02.2
	Dist     49980
	AppMagn  12.77
	Class   "WN5"
	MassSol  265
	Radius   46598500
}

Star	"ETA Car"
{
	RA       10 45 03.591
	Dec     -59 41 04.26
	Dist     2285
	AppMagn	 6.21
	Class   "WN8"
	MassSol  125
	Radius   97370000
}

Star	"WR 25/HD 93162"
{
	RA      10 44 10.337
	Dec    -59 43 11.41
	AppMagn 8.80
	//Lum     6.3e6	// Could break constellation shape if uncommented
	Dist    2290
	Class   WN6 // O2.5If
	MassSol 110
	RadSol  33
	Teff    50100
	Age     0.002
}

Star	"KY Cyg"
{
	RA       20 25 58.05
	Dec     +38 21 07.6
	Dist     1580
	AppMagn  10.57
	Class   "M3 Ia"
	MassSol  25
	Radius   1484892.5
}

Star	"La Superba/Y CVn/HD 110914/HIP 62223"
{
	RA       12 45 07.80
	Dec     +45 26 25.0
	Dist     217.87
	AppMagn	 7.4
	Class   "C7 Ib"
	MassSol  3
	Radius   149532500 //km (R*695500)
	FeH      1
}

Star	"Pistol Star/V4647 Sgr"
{
	RA       17 46 15
	Dec     -28 50 04
	Dist     7770
	AppMagn	 20
	Class   "M1 Ia"
	MassSol  150
	Radius   345000000
}

Star	"V509 Cas/HD 217476/HIP 113561"
{
	RA       23 00 05.1
	Dec     +56 56 43
	Dist     2400
	AppMagn	 5.1
	Class   "G0 Ia"
	Radius   278200000
	MassSol  25
}

Star	"T Cep/HIP 104451/HIC 104451"
{
	RA       21 09 31.7819
	Dec     +68 29 27.206
	Dist     210
	AppMagn	 7.37
	Class   "M7 III"
	MassSol  16
	Radius   403390000
}

Star	"WOH G64/2MASS J04551048-6820298"
{
	RA       04 55 10.49
	Dec     -68 20 29.08
	Dist     50000
	AppMagn	 7.37
	Class   "M7 Ib"
	MassSol  19
	Radius   1231035000
}

Star	"V354 Cep/IRC +60361/IRAS 22317+5838/2MASS J22333464+5853470"
{
	RA       22 33 34.643
	Dec     +58 53 47.05
	Dist     2759.3
	AppMagn	 10.82
	Class   "M2 Ia"
	MassSol  19
	Radius   1057160000
}

Star	"KW Sgr/HD 316496/HIP 87433"
{
	RA       17 52 00.7257
	Dec     -28 01 20.562
	Dist     3065
	AppMagn	 8.983
	Class   "M2 Ia"
	MassSol  19
	Radius   1015430000
}

Star	"V838 Mon/Nova Monocerotis 2002/GSC 04822-00039"
{
	RA       07 04 04.85
	Dec     -03 50 50.1
	Dist     5800
	AppMagn  15.74
	Class   "B3 V"
	MassSol  19
	Radius   1091935000
}

Star	"S Peg/HD 220033/HIP 115242/AAVSO 2315+08"
{
	RA       23 20 32.6145
	Dec     +08 55 08.143
	Dist     324
	AppMagn  6.9
	Class   "M6 0"
    MassSol  16.5
    Radius   403390000
}

Star	"S Dor/HD 35343/AAVSO 0518-69"
{
	RA       05 18 14.35
	Dec     -69 15 01.1
	Dist     49925
	AppMagn	 9.721
	Class   "A0 0"
	MassSol  60.0
	Radius   271440000
}

// Supergiant star catalog by RockoRocks

Star	"NML Cyg/V1489 Cyg"
{
    RA       20 46 25.6
    Dec     -40 06 59.4
    Dist     1610
    Class   "M6 Ia"
    AppMagn  16.6
	Radius   573787500
	MassSol  33
}

Star	"UY Sct"
{
    RA       18 27 36.5
    Dec     -12 27 58.8
	Dist     2900
    Class   "M4 Ia"
    AppMagn  9.0
    RadSol   1708
    MassSol  10
}

Star	"W1-26/W26/Wd 1-26/Westerlund 1-26/Westerlund 1 BKS A"
{
    RA       16 47 03.99
    Dec     -45 50 36.00
    Dist     5500
    Class   "M4 Ia" // median spectral type estimate
    AppMagn  16.79
	Radius   708366750
	MassSol  34 // fictional
}

Star	"VX Sgr/HIP 88838/HD 165674"
{
    RA       18 08 04.0485
    Dec     -22 13 26.614
    Dist     1570
    Class   "M5 III"
    AppMagn  10.35 // actually a semiregular variable star, this is the average appmagn
	Radius   528580000 // maximum possible size, likely less
	MassSol  10.5 // fictional
}

Star	"AH Sco/HIP 84071/HD 155161"
{
    RA       17 11 17.02114
    Dec     -32 19 30.7132
    Dist     3679.138
    Class   "M5 Ia"
    AppMagn  12.5
	Radius   490675250
	MassSol  30 // fictional
}

Star	"PZ Cas/HIP 117078"
{
    RA       23 44 03.28104
    Dec     -61 47 22.1823
    Class   "M3 Ia"
    AppMagn  8.75
	Radius   544228750
	MassSol  29 // fictional
}

Star	"BI Cyg/IRC +40408"
{
    RA       20 21 21.8803
    Dec      36 55 55.771
    Class   "M3 Ia"
    AppMagn  9.31
	Radius   468919688
	MassSol  31 // fictional
}

Star	"S Per/HD 14528/HIP 11093"
{
    RA       02 22 51.709
    Dec      58 31 11.45
    Class   "M3 Ia"
    AppMagn  9.23
	Radius   349488750
	MassSol  28 // fictional
}

Star	"BC Cyg/HIP 100404/BD+37 3903"
{
    RA       02 01 05.98
    Dec      75 29 00.04
    Dist     3150
    Class   "M3 Ia"
    AppMagn  9.97
	Radius   443381250
	MassSol  20
}

Star	"RT Car/HIP 52562/HIP 303310/SAO 238424"
{
    RA       10 44 47.14
    Dec     -59 24 48.12
    Dist     1966
    Class   "M2 Ia"
    AppMagn  8.55
	Radius   379047500
	MassSol  25 // fictional
}

Star	"V396 Cen/HD 115283/SAO 252241"
{
    RA       13 17 25.0446
    Dec     -61 35 02.376
    Dist     5672
    Class   "M4 Ia"
    AppMagn  7.92
}

Star	"CK Car/HD 90382/SAO 238038"
{
    RA       10 24 25.3580
    Dec     -60 11 29.039
    Dist     2176.8234
    Class   "M3 Ia"
    AppMagn  7.59
	RadSol   1060
	Teff     3550
}

Star	"V1749 Cyg/IRC +40406/GSC 02680-00828"
{
    RA       20 21 14.068
    Dec      35 37 16.63
    Class   "M2 Ia"
    AppMagn  9.84
	RadSol   800
}

Star	"RS Per/HD 14488/BD+56 583"
{
    RA       02 22 24.30
    Dec      57 06 34.4
    Class   "M4 Ia"
    AppMagn  8.73
	Dist     2940	// Chi Persei cluster
	MassSol  15
	RadSol   800
	Teff     3470
}

///////////////////////////////////////////////////////////
//               Some nearest red dwarfs                 //
///////////////////////////////////////////////////////////

Star	"Kapteyn/Kapteyn's Star/VZ Pic/GJ 191/HD 33793/HIP 24186"
{
	RA       05 11 40.58112
	Dec     -45 01 06.2899
	Dist     3.91
	Class   "M1 VI"
	AppMagn  8.853
	MassSol  0.281
	RadSol   0.29
	Teff     3550
	FeH     -0.86
	Age      8
	//RotationPeriod 38.64411	// must be in the planets catalog
}

Star	"DX Cnc/LHS 248/Gliese 1111/GJ 1111"
{
	RA			08 29 49.345
	Dec		   +26 46 33.74
	Dist		3.63
	Class	   "M6 V"
	AppMagn		14.81
	MassSol		0.09
	Radius		76505
	Age         0.2
	//RotationPeriod	11.04	// must be in the planets catalog
}

Star	"Gliese 3622/GJ 3622/LHS 292"
{
	RA				10 48 12.6
	Dec			   -11 20 14
	Dist			4.54
	AppMagn			15.73
	Class		   "M6.5 V"
	MassSol			0.08
	Radius			76505
}

Star	"HH And/GJ 905/LHS 549"
{
	RA			23 41 54.99
	Dec		   +44 10 40.8
	Dist		3.16
	AppMagn		12.29
	Class	   "M6 V"
	MassSol		0.136
	Radius		111280
}

Star	"TZ Ari/Gliese 83.1/GJ 83.1"
{
	RA			02 00 13.2
	Dec		   +13 03 08
	Dist		4.5
	AppMagn		12.31
	Class	   "M4.5 V"
	MassSol		0.089
	Radius		347750
}

Star	"CN Leo/Wolf 359"
{
	RA			10 56 28.99
	Dec		   +07 00 52
	Dist		2.39
	Lum			0.001
	Class	   "M6 V"
	MassSol		0.09
	Radius		111280
	Age			0.225
}

Star	"AD Leo/Gliese 388/LHS 5167/LTT 12761"
{
	RA		10 19 36.277
	Dec		+19 52 12.06
	Dist	4.89
	AppMagn	9.32
	Class	"M3.5 V"
	MassSol	0.405
	Radius	271245
	Age		0.163
}

Star	"Gliese 1002/GJ 1002"
{
	RA          00 06 43.2
	Dec        -07 32 17
	Dist        4.69
	AppMagn     13.73
	Class      "M5.5 V"
}

Star	"Gliese 1061/GJ 1061/LHS 1565/LFT 295/LTT1702"
{
	RA          03 35 59.64
	Dec        -44 30 46.2
	Dist        3.68
	AppMagn     13.03
	Class      "M5.5 V"
	MassSol     0.113
	FeH         0.21
}

Star	"LHS 288"
{
	RA          10 44 21.26
	Dec        -61 12 35.4
	Dist        4.5
	AppMagn     13.92
	Class      "M V"
}

Star	"2MASS J02530084+1652532"
{
	RA          02 53 00.85
	Dec        +16 52 53.3
	Dist        3.858
	AbsMagn     17.21
	Class      "M8V"
	MassSol     0.13
	Radius      52162.5
	FeH         0.5
}

///////////////////////////////////////////////////////////
//           Messier 40 optical double star              //
///////////////////////////////////////////////////////////

Star    "M 40 A/HD 238107/TYC 3840-1031-1/SAO 28353"
{
    RA       12 22 12.526
    Dec      58 04 58.552
    //Dist     500 
    Class   "G0 V"
    AppMagn  9.64
}

Star    "M 40 B/HD 238108/TYC 3840-564-1/SAO 28355"
{
    RA       12 22 18.9984
    Dec      58 05 10.279
    //Dist     510 
    Class   "F8 V"
    AppMagn  10.11
}

///////////////////////////////////////////////////////////
//            Stars near Orion's Nebulae                 //
///////////////////////////////////////////////////////////

Star    "41 Ori A/TET1 Ori A"
{
    RA       05 35 15.829
    Dec     -05 23 14.36
    Dist     428.022 
    Class   "B0V"
    AppMagn  6.73   
}

Star    "41 Ori B/TET1 Ori B"
{
    RA       05 35 16.112
    Dec     -05 23 06.89
    Dist     428.031 
    Class   "B1V"
    AppMagn  7.96
}

Star    "41 Ori C/TET1 Ori C"
{
    RA       05 35 16.46375
    Dec     -05 23 22.8486
    Dist     428.034 
    Class   "O7V"
    AppMagn  5.13
}

Star    "41 Ori D/TET1 Ori D"
{
    RA       05 35 17.19248
    Dec     -05 23 15.5661
    Dist     428.026
    Class   "B1V"
    AppMagn  6.70
}

Star    "41 Ori E/TET1 Ori E"
{
    RA       05 35 15.773
    Dec     -05 23 10.02
    Dist     428.017
    Class   "G2IV"
    AppMagn  10.3
}

Star    "41 Ori F/TET1 Ori F"
{
    RA       05 35 16.72360
    Dec     -05 23 25.1688
    Dist     428.019
    Class   "B8"
    AppMagn  10.2
}

Star    "41 Ori G/TET1 Ori G"
{
    RA       05 35 16.723
    Dec     -05 23 16.56
    Dist     428.026 
    Class   "K1"
    AppMagn  14.5
}


///////////////////////////////////////////////////////////
//               Pleiades Cluster                        //
///////////////////////////////////////////////////////////

// Alcyone is in binary stars catalog

Star    "Electra"
{
    RA       03 44 52.537
    Dec     +24 06 48.01
    Dist     113.36
    Class   "B6III"
    AppMagn  3.71
}

Star    "Caleano"
{
    RA       03 44 48.215
    Dec     +24 17 22.09
    Dist     112.56
    Class   "B7IV"
    AppMagn  5.42   
}

Star    "Taygeta"
{
    RA       03 45 12.496
    Dec     +24 28 02.21
    Dist     112.42 
    Class   "B6IV"
    AppMagn  4.30   
}

Star    "Asterope"
{
    RA       03 45 54.477
    Dec     +24 33 16.24
    Dist     112.862
    Class   "B8V"
    AppMagn   5.71   
}

Star    "Maia"
{
    RA       03 45 49.607
    Dec     +24 22 03.89
    Dist     112.03     // Distance from the Sun
    Class   "B8III"
    AppMagn  3.85   
}

Star    "Pleione"
{
    RA        	03 49 11.216
    Dec     +24 08 12.16
    Dist     112.87 
    Class   "B0V"
    AppMagn  6.73   
}

Star    "Merope"
{
    RA       03 46 19.574
    Dec    +23 56 54.08
    Dist     112.013 
    Class   "B6IV"
    AppMagn   4.16   
}

Star    "Atlas"
{
    RA       03 49 09.743
    Dec     +24 03 12.30
    Dist     112.16
    Class   "B8III"
    AppMagn  3.62   
}

Star    "HIP 17527"
{
    RA       03 45 09.740
    Dec     +24 50 21.34
    Dist     112.274
    Class   "B8V"
    AppMagn  5.64
}

Star    "HIP 17692"
{
    RA       03 47 20.969
    Dec     +23 48 12.05
    Dist     112.976
    Class   "A1V"
    AppMagn  7.01
}

Star    "HIP 17704"
{
    RA       03 47 29.454
    Dec     +24 17 18.04
    Dist     112.050
    Class   "A0V"
    AppMagn  6.74
}

Star    "HIP 17776"
{
    RA       03 48 20.816
    Dec     +23 25 16.50
    Dist     111.373
    Class   "B8V"
    AppMagn   5.42
}

Star    "HIP 17862"
{
    RA       03 49 21.749
    Dec     +24 22 51.43
    Dist     113.469
    Class   "B9.5V"
    AppMagn  6.57
}

Star    "HIP 17900"
{
    RA       03 49 43.531
    Dec     +23 42 42.68
    Dist     112.655
    Class   "B8V"
    AppMagn   6.18
}

Star    "HIP 17923"
{
    RA       03 49 58.054
    Dec     +23 50 55.30
    Dist     113.873
    Class   "A0V"
    AppMagn  6.3
}

Star    "HIP 17999"
{
    RA       03 50 52.429
    Dec     +23 57 41.31
    Dist     112.73
    Class   "A2V"
    AppMagn  6.81
}

Star    "HD 23665"
{
    RA      03 47 42.078
    Dec     +23 32 37.85
    Dist     334.27
    Class   "K0"
    AppMagn  8.8
}

Star    "HD 23654"
{
    RA       03 47 36.961
    Dec     +23 36 32.86
    Dist     82.19
    Class   "K0"
    AppMagn  7.75
}

Star    "HD 23643"
{
    RA       03 47 26.830
    Dec     +23 40 42.01
    Dist     112.87
    Class   "A3V"
    AppMagn  7.75
}

Star    "HD 23632"
{
    RA       03 47 20.969
    Dec     +23 48 12.05
    Dist     113.62
    Class   "A1V"
    AppMagn  6.95
}

Star    "IDS 03415+2336"
{
    RA       03 47 24.41
    Dec     +23 54 52.8
    Dist     112.14
    Class   "A2V"
    AppMagn  7.25
}

Star    "HD 23512"
{
    RA       03 46 34.198
    Dec     +23 37 26.51
    Dist     112.78
    Class   "A0V"
    AppMagn  7.25
}

Star    "24 Tau"
{
    RA       03 47 21.036
    Dec     +24 06 58.58
    Dist     112.46
    Class   "A0V"
    AppMagn  6.25
}

Star    "HD 23608"
{
    RA       03 47 16.566
    Dec     +24 07 42.29
    Dist     112.92
    Class   "F3V"
    AppMagn  8.65
}

Star    "HD 23607"
{
    RA       03 47 19.357
    Dec     +24 08 20.63
    Dist     112.38
    Class   "A7V"
    AppMagn  8.15
}

Star    "HD 23863"
{
    RA       03 49 12.185
    Dec     +23 53 12.46
    Dist     111.28
    Class   "A7V"
    AppMagn  8.36
}

Star    "HD 23463"
{
    RA       03 46 13.743
    Dec     +24 11 47.83
    Dist     114.69
    Class   "K2"
    AppMagn  8.75
}

Star    "HD 23479"
{
    RA       03 46 16.006
    Dec     +24 11 23.54
    Dist     110.83
    Class   "A7V"
    AppMagn  8.28
}

Star    "HD 23489"
{
    RA       03 46 27.280
    Dec     +24 15 18.02
    Dist     111.45
    Class   "A2V"
    AppMagn  7.56
}

Star    "HD 23387"
{
    RA       03 45 37.789
    Dec     +24 20 08.23
    Dist     114.2
    Class   "A1V"
    AppMagn  7.33
}
Star    "HD 23194"
{
    RA       03 44 00.268
    Dec     +24 33 25.18
    Dist     110.35
    Class   "A5V"
    AppMagn  8.28
}

Star    "HD 23156"
{
    RA       03 43 43.246
    Dec     +24 22 28.49
    Dist     114.14
    Class   "A7V"
    AppMagn  8.49
}

Star    "HD 23246"
{
    RA       03 44 25.719
    Dec     +24 23 41.00
    Dist     113.15
    Class   "A8V"
    AppMagn  7.56
}

Star    "HD 23325"
{
    RA       03 45 06.539
    Dec     +24 15 48.67
    Dist     110.53
    Class   "A3V"
    AppMagn  8.96
}

Star    "HD 23361"
{
    RA       03 45 26.118
    Dec     +24 02 06.58
    Dist     112.73
    Class   "A3V"
    AppMagn  8.24
}

Star    "HD 23585"
{
    RA       03 47 04.186
    Dec     +23 59 43.01
    Dist     111.12
    Class   "F0V"
    AppMagn  8.67
}

Star    "HD 23326"
{
    RA       03 45 05.284
    Dec     +23 42 09.64
    Dist     113.63
    Class   "F2V"
    AppMagn  9.34
}

Star    "HD 23628"
{
    RA       03 47 24.062
    Dec     +24 35 18.39
    Dist     113.12
    Class   "A4V"
    AppMagn  7.89
}

Star    "HD 24013"
{
    RA       03 50 28.057
    Dec     +24 29 43.76
    Dist     113.92
    Class   "A2V"
    AppMagn  7.57
}

Star    "HD 23948"
{
    RA       03 49 56.595
    Dec     +24 20 56.38
    Dist     112.68
    Class   "A0V"
    AppMagn  7.61
}

Star    "HD 23949"
{
    RA       03 49 54.695
    Dec     +24 13 06.00
    Dist     111.65
    Class   "A0V"
    AppMagn  9.31
}

Star    "HD 23886"
{
    RA       03 49 25.983
    Dec     +24 14 51.74
    Dist     113.32
    Class   "A3V"
    AppMagn  8.15
}

Star    "HD 23872"
{
    RA       03 49 16.802
    Dec     +24 23 46.08
    Dist     112.23
    Class   "A2V"
    AppMagn  7.64
}

Star    "HD 23733"
{
    RA       03 48 13.556
    Dec     +24 19 06.33
    Dist     111.3
    Class   "A9V"
    AppMagn  8.73
}

Star    "HD 23627A"
{
    RA       03 47 26.538
    Dec     +24 39 30.45
    Dist     112.68
    Class   "A2V"
    AppMagn  8.99
}

Star    "HD 23964"
{
    RA       03 49 58.054
    Dec     +23 50 55.30
    Dist     112.3
    Class   "A0V"
    AppMagn  6.8
}

Star    "HD 23409"
{
    RA       03 45 51.635
    Dec     +24 02 20.00
    Dist     112.68
    Class   "A2V"
    AppMagn  8.11
}

Star    "HD 23763"
{
    RA       03 48 30.095
    Dec     +24 20 43.89
    Dist     112.493
    Class   "A2V"
    AppMagn  6.614
}

Star    "HIP 17664"
{
    RA       03 46 59.398
    Dec     +24 31 12.45
    Dist     111.858
    Class   "B9.5V"
    AppMagn  6.76
}

