///////////////////////////////////////////////////////////
//               Stellar mass black holes                //
///////////////////////////////////////////////////////////

// Star solver log level:
// 0 - do not log
// 1 - log errors and warnings only
// 2 - log everything
LogLevel    1

RemoveStar "HDE 226868"

StarBarycenter	"SS 433"
{
	Class  "X"
	RA      19 11 49.48
	Dec     +04 58 57.8
	Dist    4907.9755
	AppMagn 14
}

StarBarycenter "Cygnus X-1"
{
	RA      19 58 21.7
	Dec     +35 12 06
	Dist    2147.2393
}

//Distance from article: The x-ray transient xte-j1859+226 in Outburst and Quiescence//
//web:arxiv.org/abs/astro-ph/0204337//

StarBarycenter "XTE J1859+226"
{
	RA      18 58 41.5
	Dec     +22 39 30
	Dist    11000.0000
}


//Distance from article: Optical Studies of the Transient Dipping X-ray Sources X1755−338 and X1658−298 in Quiescence//
//by Stefanie Wachter and Alan P. Smale//

StarBarycenter "4U 1755-338"
{
	RA  	17 58 40.0
	Dec     -33 48 27
	Dist    5000.0000 //between 4k-10k ly
}

//Distance from article: An optical candidate for 2A 0042+323 by Philip Charles, Jhon Thorstensen and Stuard Bowyer//

StarBarycenter "3U 0042+32"
{
	RA      00 44 50.4
	Dec     +33 01 17
	Dist    2300.6135  //between 5k-10k ly, galactic halo
}

//Distance from article: V4641 Sgr and KV UMa. Two black hole candidates by A. Chuprikov and I. Guirin //

StarBarycenter "XTE J1118+480"
{
	RA      11 18 10.79
	Dec     +48 02 11.2
	Dist    582.8221
}

StarBarycenter  "XTE J1650-500"
{
	RA		16 50 01
	Dec		-49 57 45
	Dist	4600 		//from wiki ger
}


StarBarycenter  "M 33 X-7"
{
	RA      1.566422
	Dec     30.694192
	Dist    861766.74
}

StarBarycenter  "IGR J17091-3624"
{
	RA		17 9 7.92
	Dec		-36 24 25.2
	Dist	8588
}

StarBarycenter  "CXOU J132527.6-430023"
{
	RA      13 25 27.6
	Dec      -43 00 23
	Dist    3470821.19  	//adjusted distance to match with SE
}

StarBarycenter "RX J0042.3+4115"
{
	RA      00 42 22.94
	Dec     +41 15 34.0
	Dist    788871.1656
}


StarBarycenter "GRO J0422+32"
{
	RA      04 21 46.9
	Dec     +32 54 36
	Dist    1993.8650
}

StarBarycenter "LMC X-3"
{
	RA      05 38 56.4
	Dec     -64 05 01
	Dist    49970
}

StarBarycenter "LMC X-1"
{
	RA      05 39 38.7
	Dec     -69 44 36
	Dist    49970
}

StarBarycenter "A 0620-00"
{
	RA      06 22 44.5
	Dec     -00 20 45
	Dist    828.2209
}

StarBarycenter "GRS 1009-45"
{
	RA      10 13 36.3
	Dec     -45 04 32
	Dist    3067.4847
}

StarBarycenter "GRS 1124-683"
{
	RA      11 26 26.7
	Dec     -68 40 33
	Dist    920.2454
}

StarBarycenter "4U 1543-47"
{
	RA      15 47 08.6
	Dec     -47 40 09
	Dist    3987.7301
}

StarBarycenter "XTE J1550-564"
{
	RA      15 50 58.78
	Dec     -56 28 35.0
	Dist    2607.3620
}

StarBarycenter "Nor X-1"
{
	RA      16 34 01.61
	Dec     -47 23 32
	Dist    10122.6994
}

StarBarycenter "GRO J1655-40"
{
	RA      16 54 00.2
	Dec     -39 50 45
	Dist    3067.4847
}

StarBarycenter "GRS 1659-487"
{
	RA      17 02 49.4
	Dec     -48 47 22
	Dist    3987.7301
}


StarBarycenter "H 1705-250"
{
	RA      17 08 14.2
	Dec     -25 05 32
	Dist    10122.6994
}

StarBarycenter "GRS 1716-249"
{
	RA      17 19 36.9
	Dec     -25 01 03
	Dist    2300.6135
}

StarBarycenter "EXS 1737.9-2952"
{
	RA      17 41 06
	Dec     -29 53
	Dist    9202.4540
}

StarBarycenter "GRS 1739-278"
{
	RA      17 42 00.03
	Dec     -27 44 52.7
	Dist    9815.9509
}

StarBarycenter "1E 1740.7-2942"
{
	RA      17 44 03
	Dec     -29 43 25
	Dist    9202.4540
}

StarBarycenter "GRS 1758-258"
{
	RA      18 01 12.3
	Dec     -25 44 36
	Dist    7975.4601
}

StarBarycenter "XTE J1819-254"
{
	RA      18 19 21.58
	Dec     -25 24 25.1
	Dist    9815.9509
}

StarBarycenter "GRS 1915+105"
{
	RA      19 15 11.6
	Dec     +10 56 45
	Dist    11963.1902
}

StarBarycenter "GS 2000+250"
{
	RA      20 02 49.6
	Dec     +25 14 11
	Dist    1993.8650
}

StarBarycenter "GS 2023+338"
{
	RA      20 24 03.8
	Dec     +33 52 04
	Dist    2453.9877
}

///////////////////////////////////////////////////////////
//          Isolated stellar mass black holes            //
///////////////////////////////////////////////////////////

StarBarycenter "SN 1997D bar"
{
	RA       4.1835
	Dec      -56.4833
	Dist     21027000 			//Inside NGC 1536 galaxy
	Class 	 "X"
	MassSol  3
	Radius   27.5
}

StarBarycenter "MACHO-98-BLG-6 bar"
{
	RA      17 57 32.8
	Dec     -28 42 45
	Dist    1993.8650
	Class   "X"
	MassSol 6
	Radius  55
}

StarBarycenter "MACHO-96-BLG-5 bar"
{
	RA      18 08 2.5
	Dec     -27 42 17
	Dist    1993.8650
	Class   "X"
	MassSol 6
	Radius  55
}

// Distance data from adsabs.harvard.edu/abs/2001cxo..prop.1121, Christopher Reynolds article
StarBarycenter "MACHO-99-BLG-22 bar"
{
	RA      18 05 5.35
	Dec     -28 34 42.5
	Dist    500
	Class   "X"
	MassSol 50 			//between 3 and 100 Ms
	Radius  900		    //between 60-1900 Rs
}

///////////////////////////////////////////////////////////
//               Medium-mass black holes                 //
///////////////////////////////////////////////////////////

Star "M 110 Central Black Hole"
{
	CenterOf "M 110"
	Class	 "X"
    RA       0.6728
	Dec      41.6853
	Dist     760669.61
	MassSol  90000
}

Star "Meyoll II Central Black Hole"
{
	CenterOf "Meyoll II"
	Class	 "X"
	RA       0.542929
	Dec      39.645456
	Dist     788800
	MassSol  20000
}

Star "NGC 253 Central Black Hole"
{
	CenterOf "NGC 253"
	Class	 "X"
    RA       8.1897
	Dec      3.6331
	Dist     57211184.7
	MassSol  5050
}

Star "M 33 Central Black Hole"
{
	CenterOf "M 33"
	Class	 "X"
    RA       1.566422
	Dec      30.694192
	Dist     862766.74
	MassSol  50000
}

Star "NGC 1313 X-1"	// not a central black hole
{
 	Class    "X"
    RA       03 18 19.99
    Dec     -66 29 10.97
	Dist     4091583.15
	MassSol  5500
}
Star "NGC 1313 X-2"	// not a central black hole, closer to nucleus
{
	Class    "X"
    RA       03 18 16
    Dec     -66 29 53
    Dist     4093283.16
	MassSol  5500
}

Star "M 82 Central Black Hole"
{
	CenterOf "M 82"
	Class	 "X"
    RA    	 09 55 54
    Dec    	 +69 40 57
    Dist     3709835.66
	MassSol  460
}

Star "M 15 Central Black Hole"
{
	CenterOf "M 15"
	Class	 "X"
    RA       21.4995278
	Dec      12.1669444
	Dist     10300
	MassSol  3200
}

///////////////////////////////////////////////////////////
//               Supermassive black holes                //
///////////////////////////////////////////////////////////

Star "M 32 Central Black Hole"
{
	CenterOf "M 32"
	Class    "X"
	RA       0.7114
	Dec      40.8658
	Dist     847130.243
	MassSol  2500000
}

Star "M 31 Central Black Hole"
{
	CenterOf "M 31"
	Class    "X"
	RA       0.7122
	Dec      41.2689
	Dist     788876.625
	MassSol  45000000
}

Star "NGC 821 Central Black Hole"
{
	CenterOf "NGC 821"
	Class    "X"
	RA       2.1392
	Dec      10.9942
	Dist     23926907
	MassSol  37000000
}

Star "3C 66B Central Black Hole"  //Eliptical Fananoff Radio Active galaxy
{
	CenterOf "3C 66B"
	Class    "X"
	RA       02 23 11.4
	Dec      +43 00 31
	Dist     85889570.5521
	MassSol  1000000000
}

Star "NGC 1023 Central Black Hole"
{
	CenterOf "NGC 1023"
	Class    "X"
	RA       2.6733
	Dec      39.0633
	Dist     10571498.7
	MassSol  44000000
}

Star "M 77 Central Black Hole"
{
	CenterOf "M 77"
	Class    "X"
	RA       2.7111
	Dec      -0.0128
	Dist     18999877.4
	MassSol  15000000
}

Star "NGC 1097 Central Black Hole"
{
	CenterOf "NGC 1097"
	Class    "X"
	RA       2.7719
	Dec      -30.2756
	Dist     13796909.5
	MassSol  5000000
}

Star "Fornax A Central Black Hole/NGC 1316A Central Black Hole"
{
	CenterOf "NGC 1316A"
	Class    "X"
	RA       3.3936
	Dec      -36.9039
	Dist     166114790
	MassSol  10000000 //generic
}

Star "NGC 1566 Central Black Hole"
{
	CenterOf "NGC 1566"
	Class    "X"
	RA      4.3333
	Dec     -54.9372
	Dist    13796909.5
	MassSol  10000000 //generic
}

Star "PKS 0521-365 Central Black Hole"
{
	CenterOf "PKS 0521-365"
	Class    "X"
	RA       05 22 57.98
	Dec      -36 27 30.9
	Dist     205521472.3926
	MassSol  450000000
}

Star "PKS 0548-322 Central Black Hole" //BL Lacertae blazar
{
	CenterOf "PKS 0548-322"
	Class    "X"
	RA      05 50 40.5
	Dec     -32 16 16
	Dist    257668711.6564
	MassSol  140000000
}

Star "EXO 0706.1+5913 Central Black Hole" //BL Lacertae blazar
{
	CenterOf "EXO 0706.1+5913"
	Class    "X"
	RA       07 10 30.1
	Dec      +59 08 21
	Dist     460122699.3865
	MassSol  180000000
}

Star "APM 08279+5255 Central Black Hole" //BAL quasar
{
	CenterOf "APM 08279+5255"
	Class    "X"
	RA       08 31 41.60
	Dec      +52 45 16.8
	Dist     3680981595.0920
	MassSol  10000000 //generic
}

Star "NGC 2778 Central Black Hole"
{
	CenterOf "NGC 2778"
	Class    "X"
	RA       9.2067
	Dec      35.0278
	Dist     22899803.8
	MassSol  14000000
}

Star "NGC 2787 Central Black Hole"
{
	CenterOf "NGC 2787"
	Class    "X"
	RA       9.3217
	Dec      69.2036
	Dist     7480990.92
	MassSol  41000000
}

Star "M 81 Central Black Hole/NGC 3031 Central Black Hole"
{
	CenterOf "M 81"
	Class    "X"
	RA      9.9258
	Dec     69.0672
	Dist    3682241.84
	MassSol  68000000
}

Star "Spindle Central Black Hole/NGC 3115 Central Black Hole"
{
	CenterOf "NGC 3115"
	Class    "X"
	RA       10.0872
	Dec      -7.7181
	Dist     10430463.6
	MassSol  1000000000
}

Star "NGC 3245 Central Black Hole"
{
	CenterOf "NGC 3245"
	Class    "X"
	RA       10.455
	Dec      28.5078
	Dist     20900784.9
	MassSol  210000000
}

Star "NGC 3377 Central Black Hole"
{
	CenterOf "NGC 3377"
	Class    "X"
	RA       10.795
	Dec      13.9858
	Dist     10988471.9
	MassSol  100000000
}

Star "M 105 Central Black Hole/NGC 3379 Central Black Hole"
{
	CenterOf "M 105"
	Class    "X"
	RA       10.7969
	Dec      12.5811
	Dist     10881162.6
	MassSol  100000000
}

Star "NGC 3384 Central Black Hole"
{
	CenterOf "NGC 3384"
	Class    "X"
	RA       10.8044
	Dec      12.6286
	Dist     11181628.6
	MassSol  16000000
}

Star "Mrk 421 Central Black Hole" //blazar galaxy BL Lacertae type
{
	CenterOf "Mrk 421"
	Class    "X"
	RA       11 04 27
	Dec      +38 12 32
	Dist     113496932.5153
	MassSol  190000000
}

Star "NGC 3608 Central Black Hole"
{
	CenterOf "NGC 3608"
	Class    "X"
	RA       11.2831
	Dec      18.1481
	Dist     22685185.2
	MassSol  190000000
}

Star "Mrk 180 Central Black Hole"  //blazar active galaxy BL Lacertae type
{
	CenterOf "Mrk 180"
	Class    "X"
	RA       11 36 26
	Dec      70 09 29
	Dist     168711656.4417
	MassSol  160000000
}

Star "NGC 3894 Central Black Hole"
{
	CenterOf "NGC 3894"
	Class    "X"
	RA       11.8139
	Dec      59.4164
	Dist     46081677.7
	MassSol  10000000 //generic
}

Star "NGC 3998 Central Black Hole"
{
	CenterOf "NGC 3998"
	Class    "X"
	RA       11.9653
	Dec      55.4539
	Dist     25049055.7
	MassSol  20000000
}

Star "NGC 4151 Central Black Hole"
{
	CenterOf "NGC 4151"
	Class    "X"
	RA       12.1756
	Dec      39.4067
	Dist     16911945.1
	MassSol  10000000
}

Star "M 106 Central Black Hole/NGC 4258 Central Black Hole"
{
	CenterOf "M 106"
	Class    "X"
	RA       12.3158
	Dec      47.3069
	Dist     7778390.97
	MassSol  39000000
}

Star "NGC 4261 Central Black Hole"
{
	CenterOf "NGC 4261"
	Class    "X"
	RA       12.3231
	Dec      5.8244
	Dist     31395634
	MassSol  520000000
}

Star "NGC 4350 Central Black Hole"
{
	CenterOf "NGC 4350"
	Class    "X"
	RA       12.3992
	Dec      16.6933
	Dist     21216580.8
	MassSol  10000000 //generic
}

Star "NGC 4342 Central Black Hole"
{
	CenterOf "NGC 4342"
	Class    "X"
	RA       12.3942
	Dec      7.0544
	Dist     14345719.9
	MassSol  300000000
}

Star "NGC 4791 Central Black Hole" //dist unknown in cat. using SE galaxy cat dist.
{
	CenterOf "NGC 4791"
	Class    "X"
	RA       12.9119
	Dec      8.0536
	Dist     34338974.7
	MassSol  200000000
}

Star "NGC 4945 Central Black Hole"
{
	CenterOf "NGC 4945"
	Class    "X"
	RA       13.0906
	Dec      -49.4628
	Dist     3231542.8
	MassSol  1400000
}

Star "NGC 5033 Central Black Hole"
{
	CenterOf "NGC 5033"
	Class    "X"
	RA       13.2244
	Dec      36.5933
	Dist     25134903.1
	MassSol  510000000
}

Star "Centaurus A Central Black Hole/NGC 5128 Central Black Hole"
{
	CenterOf "NGC 5128"
	Class    "X"
	RA       13.4247
	Dec      -43.0161
	Dist     3476821.19
	MassSol  240000000
}

Star "Whirlpool Central Black Hole/M 51 Central Black Hole/NGC 5194 Central Black Hole"
{
	CenterOf "M 51"
	Class    "X"
	RA       13.4978
	Dec      47.1956
	Dist     7502452.78
	MassSol  10000000 //generic
}

Star "MCG 6-30-15 Central Black Hole"
{
	CenterOf "MCG 6-30-15"
	Class    "X"
	RA       13.4892
	Dec      37.4097
	Dist     231358842
	MassSol  100000000
}

Star "NGC 5845 Central Black Hole"
{
	CenterOf "NGC 5845"
	Class    "X"
	RA       15.1
	Dec      1.6336
	Dist     25000000
	MassSol  240000000
}

Star "AP Lib Central Black Hole" //blazar BL Lacertae type
{
	CenterOf "AP Lib"
	Class    "X"
	RA       15 17 41.81
	Dec      -24 22 19.5
	Dist     184049079.7546
	MassSol  120000000
}

Star "Arp 220 Central Black Hole"
{
	CenterOf "Arp 220"
	Class    "X"
	RA       15 34 57.3
	Dec      +23 30 12
	Dist     76687116.5644
	MassSol  10000000 //generic
}

Star "NGC 6251 Central Black Hole"
{
	CenterOf "NGC 6251"
	Class    "X"
	RA       16.5419
	Dec      82.5383
	Dist     93880304.1
	MassSol  530000000
}

Star "Mrk 501 Central Black Hole"  //blazard type BL Lacertae
{
	CenterOf "Mrk 501"
	Class    "X"
	RA       16 53 53
	Dec      +39 45 36
	Dist     128834355.8282
	MassSol  1600000000
}

Star "1 Zw 187 Central Black Hole" //blazard type BL Lacertae
{
	CenterOf "1 Zw 187"
	Class    "X"
	RA       17 28 13.9
	Dec      +50 13 10
	Dist     205521472.3926
	MassSol  72000000
}

Star "3C 371 Central Black Hole"
{
	CenterOf "3C 371"
	Class    "X"
	RA       18 06 05.6
	Dec      +69 49 28.1
	Dist     190184049.0798
	MassSol  320000000
}

Star "NGC 7052 Central Black Hole"
{
	CenterOf "NGC 7052"
	Class    "X"
	RA       21.3092
	Dec      26.4472
	Dist     63189845.5
	MassSol  330000000
}

Star "PKS 2201+044 Central Black Hole" //Seyfert galaxy not present in SE
{
	CenterOf "PKS 2201+044"
	Class    "X"
	RA       22 04 17.7
	Dec      +04 40 03
	Dist     101226993.8650
	MassSol  130000000
}

Star "IC 1459 Central Black Hole"
{
	CenterOf "IC 1459"
	Class    "X"
	RA       22.9528
	Dec      -36.4625
	Dist     29700147.2
	MassSol  2500000000
}

Star "NGC 7457 Central Black Hole"
{
	CenterOf "NGC 7457"
	Class    "X"
	RA       23.0164
	Dec      30.1447
	Dist     12398822.7
	MassSol  3500000
}

Star "1ES 2344+514 Central Black Hole" //blazar BL Lacertae tye not present in SE
{
	CenterOf "1ES 2344+514"
	Class    "X"
	RA       23 47 04.8
	Dec      +51 42 18
	Dist     165644171.7791
	MassSol  630000000
}

///////////////////////////////////////////////////////////
//              Sagittarius A* black hole                //
///////////////////////////////////////////////////////////

StarBarycenter	"Sgr A*"
{
	CenterOf "Milky Way"
	Class    "X"
	Lum      1e5
	RA       17.760278
	Dec     -28.936111
	Dist     8584.74
}
