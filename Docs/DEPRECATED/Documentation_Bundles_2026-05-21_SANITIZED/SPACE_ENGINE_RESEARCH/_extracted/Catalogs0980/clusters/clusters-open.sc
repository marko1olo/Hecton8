////////////////////////////////////////////////////////////
//                                                        //
//    Catalog of open clusters of the Milky Way galaxy    //
//                    for SpaceEngine                     //
//                                                        //
// Original data from:                                    //
// http://www.astro.iag.usp.br/~wilton/                   //
// OPEN CLUSTERS AND GALACTIC STRUCTURE                   //
// A project developed by:                                //
// Wilton S. Dias (UNIFEI); Jacques Lepine (IAG-USP);     //
// Bruno S. Alessi and Andre Moitinho (UL)                //
// Reference to this catalog: please use Dias W. S.,      //
// Alessi B. S., Moitinho A. and Lepine J. R. D.,         //
// 2002, A&A 389, 871,                                    //
//                                                        //
////////////////////////////////////////////////////////////

////////////////////////////////////////////////////////////
//                                                        //
//           Clusters with additional data                //
//                                                        //
////////////////////////////////////////////////////////////

Cluster	"Pleiades/M 45/NGC 1432/Melotte 22"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       3.78333333
	Dec      24.1166667
	Dist     112.0
	Radius   2.322
	AbsMagn -3.6
	Color  ( 0 0 0 ) // no starlike particle
	CenPow   0.5
	Age      135.2
	NStars   0           // stars are in the stars catalog
	//NStars   200
	//MaxStarAppMagn 8.0 // bright stars are in the stars catalog
}

Cluster	"Hyades/Melotte 25"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       4.44833333
	Dec      15.8666667
	Dist     45
	Radius   2.162
	AbsMagn -3.0
	Age      787
	Color  ( 0 0 0 ) // no starlike particle
	NStars   0 // stars are in the stars catalog
}

Cluster	"Praesepe/M 44/NGC 2632"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       8.67333333
	Dec      19.6666667
	Dist     187
	Radius   1.904
	AbsMagn -2.0
	Age      729.5
	Color  ( 0 0 0 ) // no starlike particle
	NStars   50 // stars are in the stars catalog
	MaxStarAppMagn 7.0 // bright stars are in the stars catalog
}

Cluster	"h Per/NGC 869/OCL 350"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       2.31666667
	Dec      57.1283333
	Dist     2079
	Radius   5.443
	AppMagn  5.3
	NStars   700
	Age      11.72
	CenPow   1.2
}

Cluster	"CHI Per/NGC 884/OCL 353"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       2.37305556
	Dec      57.1258333
	Dist     2940
	Radius   7.782
	AppMagn  6.1
	NStars   600
	Age      12.59
	CenPow   1.2
}

Cluster	"Southern Pleiades/IC 2602"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       10.7161111
	Dec     -64.4
	Dist     140	// to match actual stars in the HIPPARCOS catalog
	Radius   2.342
	AbsMagn -3.0
	Age      32.14
	//Color  ( 0 0 0 ) // no starlike particle
	CenPow   0.5
	NStars   0       // stars are in the stars catalog
}

////////////////////////////////////////////////////////////
//                                                        //
//       Sagittarius star cloud - as a star cluster       //
//                                                        //
////////////////////////////////////////////////////////////

Cluster	"M 24/IC 4715/Sagittarius Star Cloud/Delle Caustiche"
{
	Galaxy  "Milky Way"
	Type    "Part"	// part of a galaxy
	RA       18 17 00
	Dec     -18 29
	Dist     3000
	Radius   300
	AppMagn  4.6
	Age      220
	NStars   0		// part of a galaxy must have no stars
}

////////////////////////////////////////////////////////////
//                                                        //
//                    Other clusters                      //
//                                                        //
////////////////////////////////////////////////////////////

Cluster	"Berkeley 58"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       0.00333333333
	Dec      60.9666667
	Dist     3003
	Radius   4.804
	Age      100
}

Cluster	"Berkeley 59"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       0.0372222222
	Dec      67.4166667
	Dist     1000
	Radius   1.454
	Age      6.31
}

Cluster	"Berkeley 104"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       0.0583333333
	Dec      63.5833333
	Dist     4365
	Radius   1.905
	Age      776.2
}

Cluster	"Blanco 1"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       0.0686111111
	Dec     -29.8333333
	Dist     269
	Radius   2.739
	Age      62.52
}

Cluster	"Alessi 20"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       0.156388889
	Dec      58.6658333
	Dist     450
	Radius   2.618
	Age      166
}

Cluster	"ASCC 1"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       0.16
	Dec      62.68
	Dist     4000
	Radius   13.96
	Age      177.8
}

Cluster	"King 13"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       0.168333333
	Dec      61.1666667
	Dist     3100
	Radius   2.254
	Age      316.2
}

Cluster	"Berkeley 60"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       0.295
	Dec      60.9333333
	Dist     4365
	Radius   1.905
	Age      158.5
}

Cluster	"ASCC 2"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       0.331111111
	Dec      55.71
	Dist     1200
	Radius   6.283
	Age      676.1
}

Cluster	"Mayer 1"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       0.365
	Dec      61.75
	Dist     1429
	Radius   1.455
}

Cluster	"King 1"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       0.367777778
	Dec      64.3805556
	Dist     1080
	Radius   3.864
	Age      3981
}

Cluster	"Stock 20"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       0.420833333
	Dec      62.6166667
	Dist     909
	Radius   0.5288
}

Cluster	"NGC 103"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       0.421111111
	Dec      61.3233333
	Dist     3026
	Radius   1.76
	Age      133.7
}

Cluster	"Berkeley 2"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       0.421666667
	Dec      60.4
	Dist     5250
	Radius   1.527
	Age      794.3
}

Cluster	"NGC 129"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       0.5
	Dec      60.2183333
	Dist     1625
	Radius   4.491
	Age      76.91
}

Cluster	"Stock 21"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       0.506944444
	Dec      57.9236111
	Dist     1111
	Radius   0.6464
}

Cluster	"ASCC 3"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       0.519166667
	Dec      55.28
	Dist     1700
	Radius   6.231
	Age      79.43
}

Cluster	"NGC 133"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       0.521944444
	Dec      63.35
	Dist     630
	Radius   0.6414
	Age      10
}

Cluster	"NGC 136"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       0.525277778
	Dec      61.51
	Dist     4093
	Radius   0.893
	Age      199.5
}

Cluster	"King 14"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       0.534166667
	Dec      63.1555556
	Dist     2960
	Radius   3.444
	Age      79.43
}

Cluster	"King 15"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       0.548333333
	Dec      61.8666667
	Dist     3162
	Radius   1.38
	Age      251.2
}

Cluster	"NGC 146"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       0.549444444
	Dec      63.3341667
	Dist     3470
	Radius   2.776
	Age      12.88
}

Cluster	"NGC 189"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       0.659722222
	Dec      61.095
	Dist     752
	Radius   0.5469
	Age      10
}

Cluster	"Stock 24"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       0.661666667
	Dec      61.95
	Dist     2818
	Radius   2.049
	Age      120.2
}

Cluster	"Dias 1"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       0.709722222
	Dec      64.0680556
	Dist     1690
	Radius   1.131
	Age      12.59
}

Cluster	"NGC 225"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       0.7275
	Dec      61.775
	Dist     657
	Radius   1.147
	Age      130
}

Cluster	"King 16"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       0.729166667
	Dec      64.1855556
	Dist     1920
	Radius   4.915
	Age      10
}

Cluster	"Berkeley 4"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       0.750277778
	Dec      64.3847222
	Dist     2460
	Radius   2.218
	Age      12.59
}

Cluster	"NGC 188"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       0.791111111
	Dec      85.255
	Dist     2047
	Radius   5.061
	Age      4285
}

Cluster	"King 2"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       0.85
	Dec      58.1833333
	Dist     5750
	Radius   4.182
	Age      6026
}

Cluster	"IC 1590"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       0.880277778
	Dec      56.6283333
	Dist     2940
	Radius   1.71
	Age      3.467
}

Cluster	"ASCC 4"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       0.886111111
	Dec      61.58
	Dist     750
	Radius   5.236
	Age      218.8
}

Cluster	"Alessi 1"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       0.890833333
	Dec      49.5666667
	Dist     302
	Radius   2.108
	Age      158.5
}

Cluster	"ASCC 5"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       0.966111111
	Dec      55.84
	Dist     1500
	Radius   3.403
	Age      10.72
}

Cluster	"Skiff J0058+68.4"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       0.974722222
	Dec      68.4688889
	Dist     1600
	Radius   5.073
	Age      1259
}

Cluster	"Berkeley 62"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       1.01666667
	Dec      63.95
	Dist     2320
	Radius   1.687
	Age      15.85
}

Cluster	"NGC 366"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       1.10722222
	Dec      62.23
	Dist     1785
	Radius   0.9087
	Age      25.7
}

Cluster	"Pfleiderer 1"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       1.13527778
	Dec      65.6472222
	Dist     7200
	Radius   5.236
	Age      1000
}

Cluster	"NGC 381"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       1.13861111
	Dec      61.5833333
	Dist     1148
	Radius   1.002
	Age      319.9
}

Cluster	"Platais 2"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       1.23055556
	Dec      32.0283333
	Dist     201
	Radius   9.831
	Age      398.1
}

Cluster	"NGC 433"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       1.25305556
	Dec      60.1266667
	Dist     2323
	Radius   0.6757
	Age      31.62
}

Cluster	"NGC 436"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       1.26611111
	Dec      58.8116667
	Dist     3014
	Radius   2.192
	Age      84.33
}

Cluster	"NGC 457"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       1.32638889
	Dec      58.2866667
	Dist     2429
	Radius   7.066
	Age      21.09
}

Cluster	"NGC 559"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       1.49305556
	Dec      63.3038889
	Dist     2170
	Radius   3.282
	Age      631
}

Cluster	"M 103/NGC 581"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       1.55638889
	Dec      60.65
	Dist     2194
	Radius   1.596
	Age      21.68
}

Cluster	"Trumpler 1"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       1.595
	Dec      61.2833333
	Dist     2563
	Radius   1.118
	Age      39.81
}

Cluster	"NGC 609"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       1.60638889
	Dec      64.5383333
	Dist     3981
	Radius   1.737
	Age      1706
}

Cluster	"NGC 637"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       1.71777778
	Dec      64.04
	Dist     2500
	Radius   1.091
	Age      10
}

Cluster	"NGC 654"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       1.73333333
	Dec      61.885
	Dist     2410
	Radius   1.753
	Age      10
}

Cluster	"NGC 659"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       1.74
	Dec      60.6733333
	Dist     1938
	Radius   1.409
	Age      35.32
}

Cluster	"Collinder 463"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       1.7625
	Dec      71.81
	Dist     702
	Radius   5.82
	Age      236
}

Cluster	"NGC 663"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       1.76916667
	Dec      61.235
	Dist     2420
	Radius   4.928
	Age      25.12
}

Cluster	"ASCC 6"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       1.78694444
	Dec      57.73
	Dist     1200
	Radius   6.283
	Age      147.9
}

Cluster	"Berkeley 5"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       1.79666667
	Dec      62.9333333
	Dist     6200
	Radius   1.804
	Age      794.3
}

Cluster	"IC 166"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       1.875
	Dec      61.8333333
	Dist     4800
	Radius   4.887
	Age      1000
}

Cluster	"Stock 4"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       1.88
	Dec      57.0666667
	Dist     1538
	Radius   2.684
}

Cluster	"Berkeley 7"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       1.90333333
	Dec      62.3666667
	Dist     2570
	Radius   1.495
	Age      3.981
}

Cluster	"NGC 752"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       1.96138889
	Dec      37.785
	Dist     457
	Radius   4.985
	Age      1122
}

Cluster	"NGC 744"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       1.97583333
	Dec      55.4733333
	Dist     1207
	Radius   0.8778
	Age      177
}

Cluster	"ASCC 7"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       1.98194444
	Dec      58.97
	Dist     2000
	Radius   8.727
	Age      22.91
}

Cluster	"Berkeley 8"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       2.01833333
	Dec      75.4833333
	Dist     3150
	Radius   2.291
	Age      3162
}

Cluster	"Stock 5"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       2.075
	Dec      64.4333333
	Dist     526
	Radius   1.836
}

Cluster	"Stock 2"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       2.24527778
	Dec      59.485
	Dist     303
	Radius   2.644
	Age      169.8
}

Cluster	"Basel 10"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       2.32444444
	Dec      58.3
	Dist     1944
	Radius   0.5655
	Age      40.55
}

Cluster	"ASCC 8"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       2.34694444
	Dec      59.61
	Dist     2200
	Radius   11.52
	Age      5.754
}

Cluster	"Berkeley 64"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       2.35166667
	Dec      65.9
	Dist     3981
	Radius   1.158
	Age      1000
}

Cluster	"Stock 6"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       2.395
	Dec      63.8666667
	Dist     1250
	Radius   2.545
}

Cluster	"Tombaugh 4"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       2.48611111
	Dec      61.795
	Dist     2170
	Radius   2.209
	Age      1000
}

Cluster	"Markarian 6"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       2.49444444
	Dec      60.7066667
	Dist     698
	Radius   0.6091
	Age      16.37
}

Cluster	"IC 1805"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       2.545
	Dec      61.45
	Dist     2344
	Radius   6.818
	Age      3.02
}

Cluster	"Czernik 8"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       2.55
	Dec      58.7333333
	Dist     1409
	Radius   0.4099
	Age      80.17
}

Cluster	"NGC 957"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       2.55583333
	Dec      57.56
	Dist     2200
	Radius   3.2
	Age      10
}

Cluster	"Czernik 9"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       2.55888889
	Dec      59.8866667
	Dist     1660
	Radius   0.7726
	Age      631
}

Cluster	"King 4"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       2.595
	Dec      59
	Dist     3172
	Radius   2.307
	Age      40.27
}

Cluster	"Trumpler 2"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       2.61472222
	Dec      55.915
	Dist     725
	Radius   1.793
	Age      89.13
}

Cluster	"Berkeley 65"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       2.65
	Dec      60.4166667
	Dist     2274
	Radius   1.654
	Age      9.886
}

Cluster	"M 34/NGC 1039"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       2.70138889
	Dec      42.7616667
	Dist     499
	Radius   2.54
	Age      177.4
}

Cluster	"NGC 1027"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       2.71194444
	Dec      61.6336111
	Dist     1030
	Radius   0.9288
	Age      251.2
}

Cluster	"Czernik 13"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       2.745
	Dec      62.35
	Dist     3961
	Radius   2.304
	Age      7.145
}

Cluster	"ASCC 9"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       2.78194444
	Dec      57.73
	Dist     2900
	Radius   8.604
	Age      6.166
}

Cluster	"IC 1848"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       2.85333333
	Dec      60.4333333
	Dist     2002
	Radius   5.241
	Age      6.918
}

Cluster	"Berkeley 66"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       3.07166667
	Dec      58.7666667
	Dist     5000
	Radius   2.909
	Age      3981
}

Cluster	"NGC 1193"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       3.09888889
	Dec      44.3833333
	Dist     4571
	Radius   1.994
	Age      5012
}

Cluster	"NGC 1252"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       3.18027778
	Dec     -57.7666667
	Dist     790
	Radius   0.9192
	Age      2818
}

Cluster	"NGC 1220"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       3.19444444
	Dec      53.345
	Dist     1800
	Radius   1.047
	Age      59.98
}

Cluster	"Trumpler 3"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       3.19666667
	Dec      63.25
	Dist     833
	Radius   1.696
}

Cluster	"NGC 1245"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       3.245
	Dec      47.2366667
	Dist     2800
	Radius   16.29
	Age      1047
}

Cluster	"King 5"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       3.24583333
	Dec      52.6866667
	Dist     2230
	Radius   4.606
	Age      1259
}

Cluster	"Stock 23"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       3.26972222
	Dec      60.1155556
	Dist     1000
	Radius   4.218
}

Cluster	"Alessi 13"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       3.36166667
	Dec     -36.3
	Dist     100
	Radius   2.91
}

Cluster	"Melotte 20"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       3.40527778
	Dec      49.8616667
	Dist     185
	Radius   8.077
	Age      71.45
}

Cluster	"Alessi-Teutsch 9"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       3.4575
	Dec      34.9533333
	Dist     700
	Radius   5.864
	Age      446.7
}

Cluster	"King 6"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       3.46388889
	Dec      56.3997222
	Dist     871
	Radius   0.6587
	Age      251.2
}

Cluster	"NGC 1342"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       3.52722222
	Dec      37.3766667
	Dist     665
	Radius   1.451
	Age      451.9
}

Cluster	"ASCC 11"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       3.53805556
	Dec      44.84
	Dist     650
	Radius   3.971
	Age      407.4
}

Cluster	"Berkeley 9"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       3.54361111
	Dec      52.6511111
	Dist     820
	Radius   0.4055
	Age      3981
}

Cluster	"NGC 1348"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       3.56833333
	Dec      51.4083333
	Dist     1820
	Radius   1.588
	Age      128.8
}

Cluster	"Berkeley 10"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       3.65888889
	Dec      66.4858333
	Dist     2290
	Radius   3.331
	Age      631
}

Cluster	"IC 348"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       3.74277778
	Dec      32.1633333
	Dist     385
	Radius   0.448
	Age      43.75
}

Cluster	"Juchert 11"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       3.78833333
	Dec      53.9097222
	Dist     3600
	Radius   2.618
}

Cluster	"Tombaugh 5"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       3.79666667
	Dec      59.05
	Dist     1750
	Radius   3.563
	Age      199.5
}

Cluster	"NGC 1444"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       3.82361111
	Dec      52.6583333
	Dist     1199
	Radius   0.6976
	Age      92.04
}

Cluster	"Juchert 9"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       3.9225
	Dec      58.3916667
	Dist     4400
	Radius   1.92
	Age      39.81
}

Cluster	"King 7"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       3.98333333
	Dec      51.8
	Dist     2200
	Radius   2.24
	Age      660.7
}

Cluster	"Alicante 1"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       3.98833333
	Dec      57.2372222
	Dist     3981
	Radius   1.158
	Age      3.981
}

Cluster	"NGC 1496"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       4.07555556
	Dec      52.6616667
	Dist     1230
	Radius   0.7156
	Age      631
}

Cluster	"NGC 1502"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       4.13055556
	Dec      62.3316667
	Dist     1080
	Radius   1.257
	Age      7.943
}

Cluster	"NGC 1513"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       4.16583333
	Dec      49.515
	Dist     1320
	Radius   1.92
	Age      128.8
}

Cluster	"NGC 1528"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       4.25638889
	Dec      51.215
	Dist     1090
	Radius   2.537
	Age      398.1
}

Cluster	"Waterloo 1"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       4.31
	Dec      52.8583333
	Dist     4400
	Radius   3.2
}

Cluster	"IC 361"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       4.31666667
	Dec      58.3
	Dist     1070
	Radius   0.9338
	Age      52.24
}

Cluster	"Berkeley 11"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       4.34333333
	Dec      44.9166667
	Dist     2200
	Radius   1.6
	Age      109.9
}

Cluster	"NGC 1545"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       4.34916667
	Dec      50.2533333
	Dist     711
	Radius   1.861
	Age      280.5
}

Cluster	"FSR756"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       4.40361111
	Dec      29.7038889
	Dist     1800
	Radius   1.1
	Age      302
}

Cluster	"NGC 1582"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       4.5275
	Dec      43.7433333
	Dist     1100
	Radius   3.84
	Age      295.1
}

Cluster	"NGC 1605"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       4.58138889
	Dec      45.27
	Dist     2559
	Radius   2.233
	Age      40.74
}

Cluster	"Berkeley 67"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       4.635
	Dec      50.75
	Dist     2450
	Radius   3.563
	Age      1000
}

Cluster	"Platais 3"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       4.69361111
	Dec      71.2366667
	Dist     161
	Radius   5.06
	Age      398.1
}

Cluster	"Berkeley 68"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       4.74166667
	Dec      42.0666667
	Dist     1678
	Radius   2.929
	Age      246
}

Cluster	"Berkeley 12"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       4.74333333
	Dec      42.6833333
	Dist     3162
	Radius   1.84
	Age      3981
}

Cluster	"NGC 1647"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       4.76527778
	Dec      19.115
	Dist     540
	Radius   3.142
	Age      143.9
}

Cluster	"Alessi 2"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       4.76722222
	Dec      55.2066667
	Dist     501
	Radius   2.186
	Age      316.2
}

Cluster	"Ruprecht 148"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       4.775
	Dec      44.7333333
	Dist     3028
	Radius   1.762
	Age      52.97
}

Cluster	"NGC 1662"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       4.8075
	Dec      10.9366667
	Dist     437
	Radius   1.271
	Age      421.7
}

Cluster	"NGC 1663"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       4.81611111
	Dec      13.1483333
	Dist     700
	Radius   1.222
	Age      1995
}

Cluster	"ASCC 12"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       4.83194444
	Dec      41.73
	Dist     500
	Radius   2.182
	Age      263
}

Cluster	"NGC 1664"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       4.85166667
	Dec      43.675
	Dist     1199
	Radius   1.569
	Age      291.7
}

Cluster	"Berkeley 13"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       4.93111111
	Dec      52.8
	Dist     2470
	Radius   3.161
	Age      1000
}

Cluster	"Czernik 19"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       4.9525
	Dec      28.7797222
	Dist     2500
	Radius   2.909
	Age      25.12
}

Cluster	"Skiff J0458+43.0"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       4.97055556
	Dec      43.0133333
	Dist     2125
	Radius   1.545
	Age      891.3
}

Cluster	"Berkeley 14"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.00333333
	Dec      43.4666667
	Dist     5500
	Radius   4
	Age      1585
}

Cluster	"Berkeley 15"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.03472222
	Dec      44.5
	Dist     1202
	Radius   1.399
	Age      2512
}

Cluster	"NGC 1708"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.0575
	Dec      52.8333333
	Dist     600
	Radius   1.745
	Age      575.4
}

Cluster	"NGC 1746"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.06388889
	Dec      23.77
	Dist     420
	Radius   2.566
}

Cluster	"NGC 1750"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.06527778
	Dec      23.6583333
	Dist     630
	Radius   1.833
	Age      199.5
}

Cluster	"NGC 1758"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.07638889
	Dec      23.7983333
	Dist     760
	Radius   0.9948
	Age      398.1
}

Cluster	"Bica 6"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.10555556
	Dec      39.1638889
	Dist     1700
	Radius   1.731
	Age      1000
}

Cluster	"Platais 4"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.12277778
	Dec      22.2783333
	Dist     276
	Radius   8.191
	Age      100
}

Cluster	"NGC 1778"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.13444444
	Dec      37.0233333
	Dist     1469
	Radius   1.709
	Age      142.9
}

Cluster	"King 17"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.14027778
	Dec      39.0844444
	Dist     2960
	Radius   2.411
	Age      794.3
}

Cluster	"NGC 1802"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.17055556
	Dec      24.1083333
	Dist     400
	Radius   1.454
	Age      457.1
}

Cluster	"NGC 1798"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.19416667
	Dec      47.6916667
	Dist     4571
	Radius   3.324
	Age      794.3
}

Cluster	"NGC 1817"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.20416667
	Dec      16.69
	Dist     1972
	Radius   4.589
	Age      409.3
}

Cluster	"ASCC 13"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.22194444
	Dec      44.58
	Dist     800
	Radius   9.774
	Age      51.29
}

Cluster	"NGC 1901"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.30305556
	Dec     -68.45
	Dist     460
	Radius   0.669
	Age      602.6
}

Cluster	"ASCC 14"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.34194444
	Dec      35.22
	Dist     1100
	Radius   4.224
	Age      407.4
}

Cluster	"Czernik 20"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.34222222
	Dec      39.5472222
	Dist     3370
	Radius   17.65
	Age      14.89
}

Cluster	"Berkeley 17"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.34333333
	Dec      30.6
	Dist     2700
	Radius   2.749
	Age      1e-006
}

Cluster	"Berkeley 18"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.37
	Dec      45.4
	Dist     5800
	Radius   10.12
	Age      4266
}

Cluster	"ASCC 15"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.37694444
	Dec      36.55
	Dist     1400
	Radius   4.887
	Age      398.1
}

Cluster	"NGC 1893"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.37888889
	Dec      33.4116667
	Dist     6000
	Radius   21.82
	Age      3.02
}

Cluster	"Berkeley 19"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.40166667
	Dec      29.6
	Dist     4831
	Radius   2.811
	Age      3090
}

Cluster	"Briceno 1"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.41
	Dec      1.8
	Dist     460
	Radius   4.978
	Age      8.511
}

Cluster	"Berkeley 69"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.41
	Dec      32.65
	Dist     2860
	Radius   1.248
	Age      891.3
}

Cluster	"ASCC 17"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.42
	Dec      30.17
	Dist     2000
	Radius   8.727
	Age      13.18
}

Cluster	"NGC 1896"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.42833333
	Dec      29.3283333
	Dist     820
	Radius   2.385
	Age      631
}

Cluster	"Berkeley 70"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.42833333
	Dec      41.9
	Dist     4168
	Radius   3.637
	Age      4677
}

Cluster	"NGC 1883"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.43166667
	Dec      46.49
	Dist     4800
	Radius   2.094
	Age      1000
}

Cluster	"Collinder 65"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.435
	Dec      16.7002778
	Dist     310
	Radius   9.923
	Age      25.7
}

Cluster	"ASCC 18"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.43611111
	Dec      0.82
	Dist     500
	Radius   5.411
	Age      13.18
}

Cluster	"Czernik 21"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.44472222
	Dec      36.0136111
	Dist     2300
	Radius   2.342
	Age      3548
}

Cluster	"Mamajek 3"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.45305556
	Dec      6.26666667
	Dist     92
	Radius   3.347
	Age      19.95
}

Cluster	"ASCC 19"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.46305556
	Dec     -1.98
	Dist     350
	Radius   4.887
	Age      43.65
}

Cluster	"Waterloo 2"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.46694444
	Dec      40.3716667
	Dist     570
	Radius   0.3316
}

Cluster	"NGC 1907"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.46805556
	Dec      35.325
	Dist     1800
	Radius   1.833
	Age      316.2
}

Cluster	"Stock 8"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.46861111
	Dec      34.4233333
	Dist     2005
	Radius   3.499
	Age      1.995
}

Cluster	"Kronberger 1"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.4725
	Dec      34.775
	Dist     1900
	Radius   0.4422
	Age      31.62
}

Cluster	"M 38/NGC 1912"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.47777778
	Dec      35.8483333
	Dist     1400
	Radius   4.072
	Age      316.2
}

Cluster	"ASCC 20"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.47888889
	Dec      1.63
	Dist     450
	Radius   5.891
	Age      22.39
}

Cluster	"ASCC 21"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.48305556
	Dec      3.65
	Dist     500
	Radius   6.982
	Age      12.88
}

Cluster	"NGC 1931"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.52361111
	Dec      34.245
	Dist     3086
	Radius   2.244
	Age      10.05
}

Cluster	"Berkeley 20"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.54361111
	Dec      0.188333333
	Dist     8400
	Radius   2.443
	Age      6026
}

Cluster	"Collinder 69"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.585
	Dec      9.93333333
	Dist     400
	Radius   4.073
	Age      5.012
}

Cluster	"NGC 1981"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.58583333
	Dec     -4.43166667
	Dist     400
	Radius   1.629
}

Cluster	"NGC 1977"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.58777778
	Dec     -4.82
	Dist     476
	Radius   1.385
}

Cluster	"NGC 1976"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.58777778
	Dec     -5.39
	Dist     414
	Radius   2.83
	Age      12.88
}

Cluster	"NGC 1980"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.59
	Dec     -5.915
	Dist     500
	Radius   1.454
}

Cluster	"Collinder 70"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.59166667
	Dec     -1.1
	Dist     387
	Radius   7.881
	Age      9.55
}

Cluster	"M 36/NGC 1960"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.605
	Dec      34.14
	Dist     1330
	Radius   1.934
	Age      25.12
}

Cluster	"Koposov 36"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.61416667
	Dec      31.2108333
	Dist     1500
	Radius   1.963
	Age      31.62
}

Cluster	"NGC 1996"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.63611111
	Dec      25.8166667
	Dist     1400
	Radius   4.48
	Age      281.8
}

Cluster	"Sigma Orionis"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.645
	Dec     -2.6
	Dist     399
	Radius   0.5803
	Age      12.88
}

Cluster	"Stock 10"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.65
	Dec      37.9333333
	Dist     380
	Radius   1.382
	Age      223.9
}

Cluster	"Koposov 27"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.65833333
	Dec      33.35
	Dist     3600
	Radius   1.571
	Age      31.62
}

Cluster	"Berkeley 71"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.68194444
	Dec      32.2777778
	Dist     3260
	Radius   2.94
	Age      1000
}

Cluster	"Koposov 77"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.73111111
	Dec      21.7102778
	Dist     1750
	Radius   1.273
	Age      4467
}

Cluster	"Teutsch 10"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.73944444
	Dec      28.8202778
	Dist     2600
	Radius   2.269
	Age      31.62
}

Cluster	"Koposov 10"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.79138889
	Dec      35.4322222
	Dist     2000
	Radius   1.164
	Age      31.62
}

Cluster	"Basel 4"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.80833333
	Dec      30.2166667
	Dist     3000
	Radius   1.571
	Age      199.5
}

Cluster	"Collinder 74"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.81111111
	Dec      7.35833333
	Dist     2510
	Radius   2.19
	Age      1288
}

Cluster	"King 8"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.82333333
	Dec      33.6333333
	Dist     6403
	Radius   3.725
	Age      415
}

Cluster	"Czernik 23"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.83444444
	Dec      28.8947222
	Dist     2500
	Radius   1.818
	Age      281.8
}

Cluster	"Berkeley 72"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.83833333
	Dec      22.2
	Dist     3500
	Radius   1.018
	Age      446.7
}

Cluster	"Berkeley 21"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.86166667
	Dec      21.7833333
	Dist     5000
	Radius   3.636
	Age      2188
}

Cluster	"Koposov 43"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.87083333
	Dec      29.9191667
	Dist     2800
	Radius   3.258
	Age      1995
}

Cluster	"M 37/NGC 2099"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.87166667
	Dec      32.5533333
	Dist     1383
	Radius   2.816
	Age      346.7
}

Cluster	"NGC 2112"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.89583333
	Dec      0.41
	Dist     940
	Radius   2.461
	Age      1778
}

Cluster	"Teutsch 51"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.89777778
	Dec      26.8297222
	Dist     3300
	Radius   1.728
	Age      794.3
}

Cluster	"Czernik 24"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.92361111
	Dec      20.8863889
	Dist     4600
	Radius   3.345
	Age      2512
}

Cluster	"Basel 11b"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.97
	Dec      21.9666667
	Dist     1800
	Radius   1.833
	Age      251.2
}

Cluster	"Berkeley 22"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       5.97416667
	Dec      7.75666667
	Dist     6000
	Radius   1.309
	Age      3311
}

Cluster	"Koposov 12"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.01555556
	Dec      35.2766667
	Dist     2050
	Radius   2.683
	Age      794.3
}

Cluster	"NGC 2129"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.01861111
	Dec      23.3222222
	Dist     2200
	Radius   1.6
	Age      10
}

Cluster	"NGC 2126"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.04138889
	Dec      49.9177778
	Dist     1090
	Radius   1.015
	Age      1259
}

Cluster	"NGC 2141"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.04861111
	Dec      10.4466667
	Dist     4033
	Radius   5.866
	Age      1702
}

Cluster	"NGC 2143"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.05194444
	Dec      5.72833333
	Dist     800
	Radius   1.28
	Age      1413
}

Cluster	"FSR 932"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.07388889
	Dec      14.5555556
	Dist     1500
	Radius   1.462
	Age      151.4
}

Cluster	"IC 2157"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.08055556
	Dec      24.0558333
	Dist     2040
	Radius   1.484
	Age      63.1
}

Cluster	"IC 2156"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.08083333
	Dec      24.1583333
	Dist     2100
	Radius   1.222
	Age      251.2
}

Cluster	"ESO 425-06"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.08055556
	Dec     -29.1830556
	Dist     1100
	Radius   0.7999
	Age      2512
}

Cluster	"FSR 942"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.09944444
	Dec      13.6683333
	Dist     3100
	Radius   4.013
	Age      1000
}

Cluster	"NGC 2158"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.12361111
	Dec      24.0966667
	Dist     5071
	Radius   3.688
	Age      1054
}

Cluster	"NGC 2169"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.14
	Dec      13.965
	Dist     1052
	Radius   0.765
	Age      11.67
}

Cluster	"Kharchenko 1"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.14666667
	Dec      24.3316667
	Dist     2520
	Radius   2.566
	Age      100
}

Cluster	"M 35/NGC 2168"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.14833333
	Dec      24.3333333
	Dist     912
	Radius   5.306
	Age      177.8
}

Cluster	"Koposov 53"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.14888889
	Dec      26.2636111
	Dist     3450
	Radius   2.509
	Age      35.48
}

Cluster	"Dias 2"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.15305556
	Dec      4.59305556
	Dist     2835
	Radius   4.536
	Age      794.3
}

Cluster	"DC 8"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.15583333
	Dec      31.2316667
	Dist     2100
	Radius   0.2443
	Age      1000
}

Cluster	"Platais 5"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.16
	Dec     -22.1516667
	Dist     272
	Radius   11.88
	Age      60.26
}

Cluster	"NGC 2175"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.16083333
	Dec      20.4866667
	Dist     1627
	Radius   5.206
	Age      8.974
}

Cluster	"NGC 2180"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.16333333
	Dec      4.80722222
	Dist     910
	Radius   1.324
	Age      707.9
}

Cluster	"Koposov 63"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.16722222
	Dec      24.5605556
	Dist     3000
	Radius   2.182
	Age      1413
}

Cluster	"FSR 923"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.17666667
	Dec      16.9711111
	Dist     1500
	Radius   1.942
	Age      501.2
}

Cluster	"Pismis 27"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.18166667
	Dec      20.6072222
	Dist     1000
	Radius   0.7272
	Age      31.62
}

Cluster	"NGC 2184"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.18333333
	Dec     -3.48333333
	Dist     640
	Radius   3.258
	Age      234.4
}

Cluster	"NGC 2186"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.20194444
	Dec      5.45833333
	Dist     1445
	Radius   1.051
	Age      54.7
}

Cluster	"NGC 2194"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.22916667
	Dec      12.8066667
	Dist     3781
	Radius   4.949
	Age      327.3
}

Cluster	"Ferrero 11"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.2425
	Dec      0.641666667
	Dist     1500
	Radius   4.712
	Age      263
}

Cluster	"ESO 425-15"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.24305556
	Dec     -29.375
	Dist     990
	Radius   0.8639
	Age      1000
}

Cluster	"Skiff J0614+12.9"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.24638889
	Dec      12.8708333
	Dist     3150
	Radius   1.374
	Age      251.2
}

Cluster	"NGC 2192"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.25472222
	Dec      39.855
	Dist     3631
	Radius   2.641
	Age      1259
}

Cluster	"Platais 6"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.25722222
	Dec      3.845
	Dist     348
	Radius   12.76
	Age      60.26
}

Cluster	"NGC 2204"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.25916667
	Dec     -18.665
	Dist     2629
	Radius   3.824
	Age      787
}

Cluster	"NGC 2202"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.28055556
	Dec      5.99666667
	Dist     900
	Radius   0.9163
	Age      549.5
}

Cluster	"Collinder 89"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.3
	Dec      23.6333333
	Dist     667
	Radius   5.821
}

Cluster	"Koposov 62"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.30055556
	Dec      24.7105556
	Dist     2800
	Radius   2.443
	Age      2512
}

Cluster	"ASCC 23"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.33888889
	Dec      46.67
	Dist     600
	Radius   3.77
	Age      281.8
}

Cluster	"NGC 2215"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.34694444
	Dec     -7.28333333
	Dist     1293
	Radius   1.316
	Age      233.9
}

Cluster	"Berkeley 73"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.36666667
	Dec     -6.35
	Dist     9800
	Radius   2.851
	Age      1514
}

Cluster	"Bochum 1"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.42361111
	Dec      19.7666667
	Dist     2803
	Radius   10.6
	Age      4.853
}

Cluster	"FSR 948"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.43138889
	Dec      15.8375
	Dist     2900
	Radius   2.109
	Age      30.2
}

Cluster	"NGC 2225"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.44333333
	Dec     -9.63833333
	Dist     3200
	Radius   1.303
	Age      1288
}

Cluster	"NGC 2232"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.45416667
	Dec     -4.75833333
	Dist     359
	Radius   2.767
	Age      53.33
}

Cluster	"ASCC 24"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.47888889
	Dec     -7.02
	Dist     400
	Radius   2.443
	Age      9.12
}

Cluster	"NGC 2243"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.49277778
	Dec     -31.2833333
	Dist     4458
	Radius   3.242
	Age      1076
}

Cluster	"NGC 2236"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.49416667
	Dec      6.83
	Dist     2930
	Radius   2.983
	Age      345.1
}

Cluster	"Collinder 96"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.505
	Dec      2.86666667
	Dist     962
	Radius   1.679
	Age      10.74
}

Cluster	"Czernik 26"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.51333333
	Dec     -4.21666667
	Dist     8872
	Radius   6.452
	Age      1000
}

Cluster	"Collinder 95"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.51416667
	Dec      9.94361111
	Dist     556
	Radius   2.183
}

Cluster	"Collinder 97"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.52166667
	Dec      5.91666667
	Dist     630
	Radius   2.291
	Age      100
}

Cluster	"NGC 2244"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.53194444
	Dec      4.94166667
	Dist     1445
	Radius   6.095
	Age      7.87
}

Cluster	"FSR 974"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.54472222
	Dec      12.5319444
	Dist     2600
	Radius   4.538
	Age      398.1
}

Cluster	"NGC 2240"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.55277778
	Dec      35.25
	Dist     450
	Radius   0.7199
	Age      3162
}

Cluster	"Berkeley 23"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.55833333
	Dec      20.55
	Dist     6918
	Radius   4.025
	Age      794.3
}

Cluster	"Basel 8"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.57
	Dec      8.08333333
	Dist     1328
	Radius   5.601
	Age      126.5
}

Cluster	"NGC 2251"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.57722222
	Dec      8.36666667
	Dist     1329
	Radius   1.933
	Age      267.3
}

Cluster	"NGC 2252"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.57833333
	Dec      5.36666667
	Dist     900
	Radius   2.356
	Age      691.8
}

Cluster	"NGC 2254"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.59694444
	Dec      7.67333333
	Dist     2364
	Radius   1.719
	Age      202.8
}

Cluster	"ESO 426-26"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.605
	Dec     -30.8583333
	Dist     1240
	Radius   0.9018
	Age      1000
}

Cluster	"Ruprecht 1"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.60666667
	Dec     -14.1833333
	Dist     1000
	Radius   0.7272
}

Cluster	"Basel 7"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.61
	Dec      8.35
	Dist     1684
	Radius   1.225
	Age      107.9
}

Cluster	"Trumpler 5"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.61166667
	Dec      9.43333333
	Dist     2400
	Radius   5.376
	Age      5012
}

Cluster	"vdBergh 1"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.61666667
	Dec      3.06666667
	Dist     1687
	Radius   1.227
	Age      105.9
}

Cluster	"Collinder 106"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.61833333
	Dec      5.95
	Dist     769
	Radius   3.915
}

Cluster	"Collinder 107"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.62833333
	Dec      4.73333333
	Dist     1738
	Radius   7.331
	Age      10
}

Cluster	"Berkeley 24"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.62972222
	Dec      0.871944444
	Dist     4700
	Radius   4.785
	Age      2188
}

Cluster	"NGC 2259"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.63916667
	Dec      10.8833333
	Dist     3311
	Radius   1.445
	Age      316.2
}

Cluster	"Collinder 110"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.64
	Dec      2.01666667
	Dist     1950
	Radius   5.105
	Age      1413
}

Cluster	"NGC 2262"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.66055556
	Dec      1.14333333
	Dist     3600
	Radius   2.618
	Age      1000
}

Cluster	"NGC 2264"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.68277778
	Dec      9.895
	Dist     667
	Radius   3.783
	Age      8.995
}

Cluster	"Berkeley 25"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.68333333
	Dec     -16.5166667
	Dist     11400
	Radius   8.29
	Age      5012
}

Cluster	"Ruprecht 3"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.70194444
	Dec     -29.4541667
	Dist     1100
	Radius   0.64
	Age      1514
}

Cluster	"Dolidze 23"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.71527778
	Dec      0.0461111111
	Dist     625
	Radius   1.154
}

Cluster	"NGC 2269"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.72138889
	Dec      4.625
	Dist     1687
	Radius   0.7361
	Age      260.6
}

Cluster	"NGC 2266"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.72194444
	Dec      26.97
	Dist     3400
	Radius   2.473
	Age      631
}

Cluster	"NGC 2270"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.7325
	Dec      3.47833333
	Dist     1400
	Radius   2.851
	Age      436.5
}

Cluster	"Dolidze 25"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.75166667
	Dec      0.3
	Dist     6800
	Radius   19.78
	Age      6.31
}

Cluster	"ASCC 25"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.75888889
	Dec      24.6
	Dist     1400
	Radius   5.131
	Age      724.4
}

Cluster	"M 41/NGC 2287"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.76694444
	Dec     -20.7566667
	Dist     710
	Radius   4.027
	Age      251.2
}

Cluster	"NGC 2286"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.79444444
	Dec     -3.14833333
	Dist     2600
	Radius   5.294
	Age      199.5
}

Cluster	"NGC 2281"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.80472222
	Dec      41.0783333
	Dist     558
	Radius   2.029
	Age      358.1
}

Cluster	"Bochum 2"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.815
	Dec      0.383333333
	Dist     2661
	Radius   0.5805
	Age      4.624
}

Cluster	"Ruprecht 4"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.815
	Dec     -10.5333333
	Dist     4700
	Radius   2.051
	Age      794.3
}

Cluster	"Berkeley 75"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.81694444
	Dec     -23.9966667
	Dist     9100
	Radius   5.294
	Age      3981
}

Cluster	"Berkeley 26"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.83833333
	Dec      5.75
	Dist     7762
	Radius   4.516
	Age      4467
}

Cluster	"ASCC 26"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.84
	Dec      7.25
	Dist     800
	Radius   2.374
	Age      123
}

Cluster	"Berkeley 27"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.855
	Dec      5.76666667
	Dist     5035
	Radius   1.465
	Age      1995
}

Cluster	"NGC 2301"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.8625
	Dec      0.46
	Dist     870
	Radius   1.772
	Age      158.5
}

Cluster	"NGC 2302"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.86527778
	Dec     -7.08333333
	Dist     1500
	Radius   1.091
	Age      12.02
}

Cluster	"Berkeley 28"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.87
	Dec      2.93333333
	Dist     2557
	Radius   1.116
	Age      70.15
}

Cluster	"Berkeley 29"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.88833333
	Dec      16.9166667
	Dist     14871
	Radius   12.98
	Age      1059
}

Cluster	"ASCC 27"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.89805556
	Dec     -4.39
	Dist     1200
	Radius   4.189
	Age      562.3
}

Cluster	"ASCC 28"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.90111111
	Dec      0.17
	Dist     800
	Radius   4.189
	Age      218.8
}

Cluster	"ASCC 29"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.905
	Dec     -1.65
	Dist     750
	Radius   2.88
	Age      114.8
}

Cluster	"NGC 2306"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.90805556
	Dec     -7.20333333
	Dist     1200
	Radius   2.094
	Age      707.9
}

Cluster	"NGC 2304"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.91972222
	Dec      17.9883333
	Dist     3991
	Radius   1.741
	Age      794.3
}

Cluster	"Ruprecht 6"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.93333333
	Dec     -13.2833333
	Dist     7691
	Radius   1.119
	Age      3162
}

Cluster	"NGC 2309"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.93416667
	Dec     -7.175
	Dist     2511
	Radius   1.826
	Age      251.2
}

Cluster	"Collinder 121"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.93888889
	Dec     -24.7294444
	Dist     1100
	Radius   9.6
	Age      12.02
}

Cluster	"ASCC 30"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.95
	Dec     -6.21
	Dist     800
	Radius   3.63
	Age      158.5
}

Cluster	"Berkeley 31"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.96
	Dec      8.26666667
	Dist     8272
	Radius   6.016
	Age      2056
}

Cluster	"Berkeley 30"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.96166667
	Dec      3.21666667
	Dist     4790
	Radius   2.09
	Age      302
}

Cluster	"Berkeley 33"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.96166667
	Dec     -13.2166667
	Dist     6000
	Radius   2.618
	Age      794.3
}

Cluster	"NGC 2311"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.96305556
	Dec     -4.61166667
	Dist     2290
	Radius   1.998
	Age      398.1
}

Cluster	"Berkeley 32"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       6.96833333
	Dec      6.43333333
	Dist     3100
	Radius   2.705
	Age      3388
}

Cluster	"Berkeley 34"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.00666667
	Dec      0.25
	Dist     7280
	Radius   2.118
	Age      2818
}

Cluster	"Tombaugh 1"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.00805556
	Dec     -20.5666667
	Dist     3000
	Radius   2.182
	Age      1000
}

Cluster	"NGC 2319"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.00888889
	Dec      3.04166667
	Dist     1100
	Radius   2.88
	Age      407.4
}

Cluster	"ASCC 31"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.015
	Dec      3.5
	Dist     600
	Radius   1.78
	Age      426.6
}

Cluster	"Alessi 33"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.03305556
	Dec     -26.5033333
	Dist     750
	Radius   6.545
	Age      151.4
}

Cluster	"M 50/NGC 2323"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.045
	Dec     -8.38333333
	Dist     950
	Radius   1.934
	Age      100
}

Cluster	"Tombaugh 2"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.05138889
	Dec     -20.8166667
	Dist     6080
	Radius   2.653
	Age      1023
}

Cluster	"ASCC 33"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.05305556
	Dec     -25.05
	Dist     800
	Radius   12.57
	Age      18.2
}

Cluster	"Czernik 27"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.05333333
	Dec      6.41666667
	Dist     4285
	Radius   1.87
	Age      1122
}

Cluster	"Bochum 3"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.05666667
	Dec     -5.05
	Dist     1762
	Radius   1.025
	Age      77.62
}

Cluster	"vdBergh 92"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.065
	Dec     -11.5333333
	Dist     1429
	Radius   0.6235
}

Cluster	"NGC 2324"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.06861111
	Dec      1.045
	Dist     3800
	Radius   5.858
	Age      446.7
}

Cluster	"Auner 1"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.07111111
	Dec     -19.75
	Dist     8900
	Radius   7.767
	Age      3236
}

Cluster	"Haffner 4"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.10333333
	Dec     -14.9833333
	Dist     4446
	Radius   3.233
	Age      1259
}

Cluster	"Berkeley 76"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.11111111
	Dec     -11.7333333
	Dist     7551
	Radius   5.491
	Age      1585
}

Cluster	"NGC 2335"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.11361111
	Dec     -10.0283333
	Dist     1417
	Radius   1.237
	Age      162.2
}

Cluster	"Ruprecht 12"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.11944444
	Dec     -28.2
	Dist     769
	Radius   0.5592
}

Cluster	"NGC 2343"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.135
	Dec     -10.6166667
	Dist     1056
	Radius   0.7679
	Age      12.71
}

Cluster	"NGC 2345"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.13833333
	Dec     -13.1933333
	Dist     2251
	Radius   3.929
	Age      71.29
}

Cluster	"Haffner 23"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.15666667
	Dec     -16.95
	Dist     588
	Radius   0.9407
}

Cluster	"Berkeley 35"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.16555556
	Dec      2.73361111
	Dist     4400
	Radius   3.84
	Age      1096
}

Cluster	"Dias 3"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.17527778
	Dec     -8.4275
	Dist     4650
	Radius   10.82
	Age      1413
}

Cluster	"ASCC 34"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.175
	Dec      6.07
	Dist     550
	Radius   2.88
	Age      354.8
}

Cluster	"Alessi 21"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.17972222
	Dec     -9.33666667
	Dist     500
	Radius   3.054
	Age      29.51
}

Cluster	"ASCC 35"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.21111111
	Dec      2.12
	Dist     800
	Radius   5.585
	Age      309
}

Cluster	"NGC 2354"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.23611111
	Dec     -25.69
	Dist     4085
	Radius   10.69
	Age      133.7
}

Cluster	"NGC 2353"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.24166667
	Dec     -10.2666667
	Dist     1119
	Radius   2.93
	Age      94.19
}

Cluster	"ASCC 36"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.24194444
	Dec     -21.12
	Dist     750
	Radius   2.356
	Age      323.6
}

Cluster	"Collinder 132"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.25555556
	Dec     -30.6833333
	Dist     472
	Radius   5.492
	Age      12.02
}

Cluster	"Berkeley 36"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.26833333
	Dec     -13.1
	Dist     6140
	Radius   4.465
	Age      3162
}

Cluster	"Alessi 3"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.27472222
	Dec     -46.685
	Dist     288
	Radius   3.016
	Age      501.2
}

Cluster	"NGC 2358"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.28222222
	Dec     -17.1166667
	Dist     630
	Radius   1.833
	Age      524.8
}

Cluster	"NGC 2355"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.28305556
	Dec      13.75
	Dist     2200
	Radius   2.24
	Age      707.9
}

Cluster	"Basel 11a"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.285
	Dec     -13.9666667
	Dist     1520
	Radius   1.105
	Age      199.5
}

Cluster	"Collinder 135"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.28805556
	Dec     -36.8166667
	Dist     316
	Radius   2.298
	Age      25.53
}

Cluster	"NGC 2360"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.29527778
	Dec     -15.6416667
	Dist     1887
	Radius   3.568
	Age      561
}

Cluster	"ASCC 37"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.30111111
	Dec     -24.48
	Dist     1600
	Radius   4.468
	Age      537
}

Cluster	"NGC 2362"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.31138889
	Dec     -24.955
	Dist     1480
	Radius   1.076
	Age      5.012
}

Cluster	"Bica 4"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.31861111
	Dec     -22.0277778
	Dist     3930
	Radius   1.429
	Age      63.1
}

Cluster	"Haffner 6"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.335
	Dec     -13.1333333
	Dist     3054
	Radius   2.665
	Age      669.9
}

Cluster	"NGC 2367"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.335
	Dec     -21.8816667
	Dist     1400
	Radius   1.018
	Age      5.012
}

Cluster	"Berkeley 37"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.34
	Dec     -1.1
	Dist     5623
	Radius   3.271
	Age      1585
}

Cluster	"Saurer 1"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.34888889
	Dec      1.80805556
	Dist     13200
	Radius   4.992
	Age      5012
}

Cluster	"King 23"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.36305556
	Dec      0.985
	Dist     3113
	Radius   3.26
	Age      891.3
}

Cluster	"Haffner 8"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.39
	Dec     -12.3333333
	Dist     1182
	Radius   0.8596
	Age      1413
}

Cluster	"Berkeley 78"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.39388889
	Dec      5.37083333
	Dist     4800
	Radius   4.189
	Age      2818
}

Cluster	"NGC 2374"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.39888889
	Dec     -13.2633333
	Dist     1468
	Radius   2.562
	Age      290.4
}

Cluster	"Collinder 140"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.4075
	Dec     -31.85
	Dist     405
	Radius   3.534
	Age      35.32
}

Cluster	"Ruprecht 18"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.41083333
	Dec     -26.2166667
	Dist     1056
	Radius   1.075
	Age      44.46
}

Cluster	"NGC 2383"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.41111111
	Dec     -20.9483333
	Dist     3400
	Radius   2.473
	Age      120.2
}

Cluster	"Haffner 9"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.41166667
	Dec     -17.0027778
	Dist     1900
	Radius   1.105
	Age      141.3
}

Cluster	"NGC 2384"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.41944444
	Dec     -21.0216667
	Dist     2900
	Radius   2.109
	Age      12.02
}

Cluster	"Waterloo 7"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.43555556
	Dec     -15.0933333
	Dist     2800
	Radius   1.059
}

Cluster	"Melotte 66"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.43972222
	Dec     -47.6666667
	Dist     4313
	Radius   8.782
	Age      2786
}

Cluster	"Ruprecht 20"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.44527778
	Dec     -28.8166667
	Dist     1208
	Radius   0.8785
	Age      316.2
}

Cluster	"ASCC 38"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.45305556
	Dec     -5.55
	Dist     500
	Radius   2.269
	Age      398.1
}

Cluster	"NGC 2395"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.45333333
	Dec      13.6083333
	Dist     512
	Radius   1.043
	Age      1175
}

Cluster	"Trumpler 7"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.45611111
	Dec     -23.95
	Dist     1474
	Radius   1.072
	Age      26.92
}

Cluster	"NGC 2396"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.46666667
	Dec     -11.7166667
	Dist     588
	Radius   0.8552
}

Cluster	"Czernik 29"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.47166667
	Dec     -15.4
	Dist     4168
	Radius   3.031
	Age      199.5
}

Cluster	"Haffner 10"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.47666667
	Dec     -15.3833333
	Dist     5248
	Radius   2.29
	Age      1259
}

Cluster	"NGC 2401"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.49
	Dec     -13.9666667
	Dist     5888
	Radius   1.713
	Age      63.1
}

Cluster	"Bochum 5"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.51666667
	Dec     -16.9333333
	Dist     872
	Radius   1.902
	Age      35.08
}

Cluster	"Czernik 30"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.52166667
	Dec     -9.96666667
	Dist     7145
	Radius   3.118
	Age      2512
}

Cluster	"Bochum 4"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.52694444
	Dec     -17.1933333
	Dist     2291
	Radius   1.666
	Age      10
}

Cluster	"Bochum 6"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.53333333
	Dec     -19.4166667
	Dist     3981
	Radius   5.79
	Age      10
}

Cluster	"ASCC 39"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.55
	Dec     -22.95
	Dist     1500
	Radius   7.854
	Age      512.9
}

Cluster	"NGC 2413"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.55194444
	Dec     -13.4
	Dist     440
	Radius   1.536
	Age      288.4
}

Cluster	"NGC 2414"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.55333333
	Dec     -15.4533333
	Dist     3455
	Radius   2.513
	Age      9.462
}

Cluster	"ESO 429-02"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.55666667
	Dec     -28.1875
	Dist     1670
	Radius   0.7287
	Age      398.1
}

Cluster	"ASCC 40"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.56
	Dec     -13.76
	Dist     700
	Radius   2.199
	Age      380.2
}

Cluster	"Haffner 11"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.59166667
	Dec     -27.6997222
	Dist     5200
	Radius   3.782
	Age      891.3
}

Cluster	"NGC 2421"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.60361111
	Dec     -20.6116667
	Dist     2200
	Radius   1.92
	Age      79.43
}

Cluster	"M 47/NGC 2422"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.60972222
	Dec     -14.4833333
	Dist     490
	Radius   1.782
	Age      72.61
}

Cluster	"Czernik 31"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.61638889
	Dec     -20.5097222
	Dist     2200
	Radius   1.6
	Age      177.8
}

Cluster	"NGC 2423"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.61833333
	Dec     -13.8716667
	Dist     766
	Radius   1.337
	Age      736.2
}

Cluster	"Ruprecht 26"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.62
	Dec     -15.65
	Dist     1250
	Radius   0.7272
}

Cluster	"Melotte 71"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.625
	Dec     -12.0666667
	Dist     3154
	Radius   3.211
	Age      235
}

Cluster	"Ruprecht 27"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.62611111
	Dec     -26.5
	Dist     1249
	Radius   0.9083
}

Cluster	"NGC 2425"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.63805556
	Dec     -14.8783333
	Dist     3550
	Radius   4.131
	Age      2188
}

Cluster	"NGC 2420"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.63972222
	Dec      21.5733333
	Dist     2480
	Radius   1.804
	Age      1995
}

Cluster	"Melotte 72"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.64
	Dec     -10.6833333
	Dist     3177
	Radius   2.31
	Age      1585
}

Cluster	"Arp-Madore 2"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.64611111
	Dec     -33.8433333
	Dist     13341
	Radius   3.881
	Age      2163
}

Cluster	"NGC 2428"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.65583333
	Dec     -16.5283333
	Dist     2100
	Radius   3.665
	Age      478.6
}

Cluster	"NGC 2430"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.66138889
	Dec     -16.2966667
	Dist     650
	Radius   1.418
	Age      478.6
}

Cluster	"ESO 493-03"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.6625
	Dec     -27.2933333
	Dist     1400
	Radius   1.425
	Age      398.1
}

Cluster	"Bochum 15"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.66833333
	Dec     -33.5333333
	Dist     2806
	Radius   1.224
	Age      5.521
}

Cluster	"Haffner 13"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.675
	Dec     -30.0833333
	Dist     714
	Radius   1.454
}

Cluster	"NGC 2439"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.67916667
	Dec     -31.6933333
	Dist     1300
	Radius   1.702
	Age      10
}

Cluster	"NGC 2432"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.68138889
	Dec     -19.0766667
	Dist     1900
	Radius   1.658
	Age      501.2
}

Cluster	"Ruprecht 151"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.68833333
	Dec     -16.25
	Dist     1250
	Radius   1.273
}

Cluster	"M 46/NGC 2437"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.69611111
	Dec     -14.81
	Dist     1510
	Radius   4.392
	Age      251.2
}

Cluster	"Ruprecht 31"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.71611111
	Dec     -35.5972222
	Dist     930
	Radius   0.4058
	Age      1000
}

Cluster	"NGC 2451A"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.72
	Dec     -38.4
	Dist     189
	Radius   3.299
	Age      60.26
}

Cluster	"NGC 2451B"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.74083333
	Dec     -37.6666667
	Dist     302
	Radius   4.744
	Age      44.46
}

Cluster	"M 93/NGC 2447"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.74166667
	Dec     -23.8566667
	Dist     1037
	Radius   1.508
	Age      387.3
}

Cluster	"NGC 2448"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.74277778
	Dec     -24.68
	Dist     1040
	Radius   3.025
	Age      15.49
}

Cluster	"Ruprecht 32"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.75277778
	Dec     -25.5333333
	Dist     5346
	Radius   3.888
	Age      12.02
}

Cluster	"Haffner 15"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.75888889
	Dec     -32.85
	Dist     2078
	Radius   0.9067
	Age      14.66
}

Cluster	"Ruprecht 34"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.76527778
	Dec     -20.3833333
	Dist     1000
	Radius   0.8727
}

Cluster	"Berkeley 39"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.77833333
	Dec     -4.6
	Dist     4780
	Radius   4.867
	Age      7943
}

Cluster	"Herschel 1"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.78388889
	Dec      0.0183333333
	Dist     370
	Radius   2.325
	Age      275.4
}

Cluster	"NGC 2453"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.79305556
	Dec     -27.195
	Dist     2150
	Radius   1.251
	Age      15.38
}

Cluster	"Ruprecht 36"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.80638889
	Dec     -26.3
	Dist     1681
	Radius   1.222
	Age      40.36
}

Cluster	"Haffner 16"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.83888889
	Dec     -25.4666667
	Dist     3165
	Radius   2.302
	Age      11.97
}

Cluster	"Czernik 32"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.84166667
	Dec     -29.8458333
	Dist     4100
	Radius   1.789
	Age      1000
}

Cluster	"Haffner 17"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.86027778
	Dec     -31.8166667
	Dist     2880
	Radius   0.8378
	Age      50.12
}

Cluster	"NGC 2477"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.86944444
	Dec     -38.53
	Dist     1300
	Radius   2.836
	Age      602.6
}

Cluster	"NGC 2467"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.87388889
	Dec     -26.4366667
	Dist     1355
	Radius   2.759
	Age      12.68
}

Cluster	"ESO 123-26"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.875
	Dec     -60.33
	Dist     550
	Radius   1.28
	Age      213.8
}

Cluster	"Haffner 18"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.8775
	Dec     -26.3833333
	Dist     6028
	Radius   4.384
	Age      7.816
}

Cluster	"Haffner 19"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.87972222
	Dec     -26.2833333
	Dist     5094
	Radius   1.482
	Age      8.59
}

Cluster	"Alessi-Teutsch 3"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.88361111
	Dec     -53.045
	Dist     800
	Radius   5.027
	Age      371.5
}

Cluster	"ASCC 43"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.885
	Dec     -28.17
	Dist     1000
	Radius   6.109
	Age      190.5
}

Cluster	"NGC 2479"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.91833333
	Dec     -17.71
	Dist     1666
	Radius   2.423
}

Cluster	"Waterloo 3"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.91833333
	Dec     -25.365
	Dist     5200
	Radius   1.513
}

Cluster	"NGC 2482"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.92
	Dec     -24.2583333
	Dist     1343
	Radius   1.953
	Age      401.8
}

Cluster	"NGC 2483"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.9275
	Dec     -27.895
	Dist     1659
	Radius   1.689
	Age      12.27
}

Cluster	"Trumpler 9"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.92777778
	Dec     -25.8833333
	Dist     2289
	Radius   1.665
	Age      100.2
}

Cluster	"NGC 2489"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.9375
	Dec     -30.0633333
	Dist     3957
	Radius   3.453
	Age      18.37
}

Cluster	"Haffner 20"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.9375
	Dec     -30.3666667
	Dist     3117
	Radius   1.36
	Age      132.1
}

Cluster	"NGC 2516"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.96777778
	Dec     -60.7533333
	Dist     409
	Radius   1.785
	Age      112.7
}

Cluster	"Ruprecht 44"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.98083333
	Dec     -28.5833333
	Dist     4730
	Radius   6.88
	Age      8.73
}

Cluster	"Ruprecht 43"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       7.98833333
	Dec     -28.9666667
	Dist     1000
	Radius   0.8727
}

Cluster	"NGC 2506"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       8.00027778
	Dec     -10.77
	Dist     3460
	Radius   6.039
	Age      1109
}

Cluster	"NGC 2509"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       8.01333333
	Dec     -19.0516667
	Dist     912
	Radius   0.7959
	Age      7943
}

Cluster	"Haffner 21"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       8.01916667
	Dec     -27.2166667
	Dist     2951
	Radius   1.288
	Age      83.18
}

Cluster	"Alessi 34"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       8.03
	Dec     -50.5583333
	Dist     1100
	Radius   7.68
	Age      77.62
}

Cluster	"Ruprecht 47"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       8.03861111
	Dec     -31.0666667
	Dist     3006
	Radius   1.749
	Age      77.62
}

Cluster	"Collinder 173"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       8.04694444
	Dec     -46.3833333
	Dist     421
	Radius   22.68
	Age      13.87
}

Cluster	"Ruprecht 49"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       8.05416667
	Dec     -26.7666667
	Dist     1823
	Radius   0.5303
	Age      85.11
}

Cluster	"NGC 2527"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       8.08277778
	Dec     -28.1466667
	Dist     601
	Radius   0.8741
	Age      445.7
}

Cluster	"ESO 430-18"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       8.11444444
	Dec     -30.8366667
	Dist     830
	Radius   1.449
	Age      1122
}

Cluster	"NGC 2533"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       8.11777778
	Dec     -29.8833333
	Dist     1700
	Radius   1.236
	Age      691.8
}

Cluster	"Pozzo 1"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       8.15888889
	Dec     -47.3366667
	Dist     336
	Radius   0.8796
}

Cluster	"NGC 2547"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       8.16916667
	Dec     -49.215
	Dist     361
	Radius   1.313
	Age      38.46
}

Cluster	"NGC 2539"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       8.17694444
	Dec     -12.8183333
	Dist     1363
	Radius   1.784
	Age      371.5
}

Cluster	"Ruprecht 53"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       8.18166667
	Dec     -27
	Dist     1000
	Radius   0.7272
}

Cluster	"NGC 2546"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       8.20416667
	Dec     -37.595
	Dist     919
	Radius   9.357
	Age      74.82
}

Cluster	"Ruprecht 55"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       8.2075
	Dec     -32.5833333
	Dist     4600
	Radius   3.345
	Age      10
}

Cluster	"Ruprecht 56"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       8.20916667
	Dec     -40.4666667
	Dist     833
	Radius   4.725
}

Cluster	"M 48/NGC 2548"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       8.22861111
	Dec     -5.75
	Dist     770
	Radius   3.36
	Age      398.1
}

Cluster	"BH 23"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       8.24
	Dec     -36.3833333
	Dist     437
	Radius   2.479
	Age      12.59
}

Cluster	"Haffner 26"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       8.26083333
	Dec     -30.8333333
	Dist     1000
	Radius   0.7272
	Age      29.51
}

Cluster	"ASCC 45"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       8.26388889
	Dec     -35.65
	Dist     3000
	Radius   10.47
	Age      13.18
}

Cluster	"ASCC 46"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       8.27611111
	Dec     -48.51
	Dist     900
	Radius   6.283
	Age      51.29
}

Cluster	"Pismis 2"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       8.29833333
	Dec     -41.6666667
	Dist     3310
	Radius   1.926
	Age      1148
}

Cluster	"Pismis 1"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       8.305
	Dec     -37.1
	Dist     5907
	Radius   2.577
	Age      84.72
}

Cluster	"NGC 2567"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       8.30888889
	Dec     -30.64
	Dist     1677
	Radius   1.707
	Age      294.4
}

Cluster	"NGC 2571"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       8.31555556
	Dec     -29.75
	Dist     1342
	Radius   1.561
	Age      30.76
}

Cluster	"Ruprecht 59"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       8.3225
	Dec     -34.4833333
	Dist     874
	Radius   0.3814
	Age      53.33
}

Cluster	"NGC 2579"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       8.34777778
	Dec     -36.2166667
	Dist     1033
	Radius   1.052
	Age      40.74
}

Cluster	"NGC 2580"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       8.35777778
	Dec     -30.3
	Dist     4000
	Radius   2.036
	Age      158.5
}

Cluster	"NGC 2588"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       8.38611111
	Dec     -32.975
	Dist     4950
	Radius   1.44
	Age      446.7
}

Cluster	"Collinder 185"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       8.38916667
	Dec     -36.3333333
	Dist     1486
	Radius   2.161
	Age      128.8
}

Cluster	"Ruprecht 61"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       8.42138889
	Dec     -34.15
	Dist     3900
	Radius   1.134
	Age      1288
}

Cluster	"Saurer 2"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       8.42444444
	Dec     -39.6338889
	Dist     6600
	Radius   2.88
	Age      1995
}

Cluster	"BH 34"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       8.52083333
	Dec     -44.5
	Dist     670
	Radius   2.436
}

Cluster	"Pismis 3"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       8.52277778
	Dec     -38.65
	Dist     1394
	Radius   1.014
	Age      1064
}

Cluster	"Alessi-Teutsch 7"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       8.52888889
	Dec     -39.1316667
	Dist     900
	Radius   7.854
	Age      75.86
}

Cluster	"ASCC 48"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       8.575
	Dec     -37.61
	Dist     400
	Radius   2.234
	Age      1230
}

Cluster	"Pismis 4"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       8.57666667
	Dec     -44.4166667
	Dist     593
	Radius   2.156
	Age      34.12
}

Cluster	"NGC 2627"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       8.62083333
	Dec     -29.955
	Dist     2000
	Radius   2.327
	Age      1413
}

Cluster	"Ruprecht 64"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       8.62222222
	Dec     -40.15
	Dist     667
	Radius   6.791
}

Cluster	"Pismis 5"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       8.62722222
	Dec     -39.5833333
	Dist     869
	Radius   0.2528
	Age      15.74
}

Cluster	"NGC 2635"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       8.64055556
	Dec     -34.77
	Dist     4000
	Radius   4.654
	Age      602.6
}

Cluster	"NGC 2645"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       8.65083333
	Dec     -46.2333333
	Dist     1668
	Radius   0.7278
	Age      19.19
}

Cluster	"Ruprecht 65"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       8.65444444
	Dec     -44.05
	Dist     664
	Radius   0.4829
}

Cluster	"IC 2391"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       8.67555556
	Dec     -53.0333333
	Dist     175
	Radius   1.527
	Age      45.81
}

Cluster	"Pismis 7"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       8.68555556
	Dec     -38.7
	Dist     4900
	Radius   2.138
	Age      501.2
}

Cluster	"Pismis 8"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       8.69333333
	Dec     -46.2666667
	Dist     1312
	Radius   0.5725
	Age      26.73
}

Cluster	"Ruprecht 67"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       8.69416667
	Dec     -43.3666667
	Dist     1504
	Radius   1.312
	Age      180.3
}

Cluster	"Mamajek 1"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       8.70166667
	Dec     -79.0272222
	Dist     97
	Radius   0.5643
	Age      7.943
}

Cluster	"IC 2395"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       8.70833333
	Dec     -48.1133333
	Dist     800
	Radius   2.164
	Age      6.31
}

Cluster	"NGC 2659"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       8.71027778
	Dec     -44.9833333
	Dist     1713
	Radius   1.246
	Age      7.762
}

Cluster	"NGC 2660"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       8.71055556
	Dec     -47.2
	Dist     2826
	Radius   1.439
	Age      1079
}

Cluster	"NGC 2658"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       8.72416667
	Dec     -32.6583333
	Dist     2021
	Radius   1.764
	Age      1419
}

Cluster	"Bochum 7"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       8.74666667
	Dec     -45.9666667
	Dist     5754
	Radius   16.74
	Age      10
}

Cluster	"Collinder 197"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       8.7475
	Dec     -41.2333333
	Dist     838
	Radius   3.047
	Age      13.43
}

Cluster	"NGC 2670"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       8.75833333
	Dec     -48.8
	Dist     1188
	Radius   1.21
	Age      48.98
}

Cluster	"NGC 2671"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       8.77
	Dec     -41.8783333
	Dist     1660
	Radius   1.449
	Age      79.43
}

Cluster	"NGC 2669"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       8.77277778
	Dec     -52.9483333
	Dist     1046
	Radius   3.043
	Age      84.53
}

Cluster	"BH 52"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       8.775
	Dec     -52.9
	Dist     667
	Radius   0.5821
}

Cluster	"Trumpler 10"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       8.79833333
	Dec     -42.45
	Dist     424
	Radius   1.788
	Age      34.83
}

Cluster	"Teutsch 38"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       8.79944444
	Dec     -38.0583333
	Dist     900
	Radius   7.069
	Age      138
}

Cluster	"Ruprecht 71"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       8.82305556
	Dec     -46.85
	Dist     1000
	Radius   0.8727
}

Cluster	"Alessi 43"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       8.83805556
	Dec     -41.72
	Dist     850
	Radius   5.934
	Age      30.2
}

Cluster	"M 67/NGC 2682"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       8.855
	Dec      11.8
	Dist     908
	Radius   3.302
	Age      2564
}

Cluster	"Ruprecht 72"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       8.86805556
	Dec     -37.6
	Dist     3019
	Radius   1.317
	Age      1259
}

Cluster	"Ruprecht 158"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       8.87416667
	Dec     -37.5666667
	Dist     4168
	Radius   1.212
	Age      1585
}

Cluster	"BH 56"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       8.95222222
	Dec     -43.25
	Dist     667
	Radius   1.94
}

Cluster	"Collinder 205"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       9.00888889
	Dec     -48.9833333
	Dist     1853
	Radius   1.348
	Age      15.85
}

Cluster	"ESO 165-09"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       9.0875
	Dec     -55.955
	Dist     450
	Radius   0.9817
	Age      912
}

Cluster	"Platais 8"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       9.15833333
	Dec     -59.1283333
	Dist     132
	Radius   18.32
	Age      60.26
}

Cluster	"ESO 166-04"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       9.175
	Dec     -53.8866667
	Dist     885
	Radius   0.5149
	Age      281.8
}

Cluster	"Platais 9"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       9.22972222
	Dec     -43.74
	Dist     174
	Radius   10.03
	Age      100
}

Cluster	"NGC 2818"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       9.26694444
	Dec     -36.625
	Dist     1855
	Radius   2.428
	Age      422.7
}

Cluster	"Pismis 11"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       9.26472222
	Dec     -50.0166667
	Dist     3600
	Radius   1.047
	Age      10
}

Cluster	"ASCC 51"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       9.3
	Dec     -69.69
	Dist     500
	Radius   5.76
	Age      338.8
}

Cluster	"NGC 2849"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       9.32305556
	Dec     -40.5233333
	Dist     6400
	Radius   3.258
	Age      631
}

Cluster	"Teutsch 48"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       9.34222222
	Dec     -52.8516667
	Dist     7000
	Radius   2.24
}

Cluster	"BH 63"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       9.34416667
	Dec     -49.2230556
	Dist     2300
	Radius   0.8363
	Age      691.8
}

Cluster	"Ruprecht 75"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       9.36527778
	Dec     -56.3166667
	Dist     4300
	Radius   1.251
	Age      1413
}

Cluster	"NGC 2866"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       9.36861111
	Dec     -51.1
	Dist     2600
	Radius   0.7563
	Age      199.5
}

Cluster	"Ruprecht 76"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       9.40361111
	Dec     -51.6666667
	Dist     1262
	Radius   0.9178
	Age      54.2
}

Cluster	"BH 66"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       9.42166667
	Dec     -54.7166667
	Dist     7000
	Radius   4.072
	Age      794.3
}

Cluster	"Ruprecht 77"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       9.45111111
	Dec     -55.1166667
	Dist     4129
	Radius   3.003
	Age      31.7
}

Cluster	"IC 2488"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       9.46055556
	Dec     -57
	Dist     1134
	Radius   2.969
	Age      129.7
}

Cluster	"ASCC 52"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       9.46611111
	Dec     -54.26
	Dist     1500
	Radius   7.069
	Age      562.3
}

Cluster	"Ruprecht 78"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       9.48611111
	Dec     -53.7
	Dist     1641
	Radius   0.716
	Age      97.05
}

Cluster	"NGC 2910"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       9.50833333
	Dec     -52.9183333
	Dist     2607
	Radius   1.517
	Age      159.6
}

Cluster	"Basel 20"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       9.51888889
	Dec     -56.4166667
	Dist     2024
	Radius   2.944
	Age      26.24
}

Cluster	"NGC 2925"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       9.55305556
	Dec     -53.3983333
	Dist     774
	Radius   1.126
	Age      70.79
}

Cluster	"Turner 5"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       9.56416667
	Dec     -36.615
	Dist     2300
	Radius   60.23
}

Cluster	"Pismis 15"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       9.57916667
	Dec     -48.0333333
	Dist     2900
	Radius   1.518
	Age      1288
}

Cluster	"ASCC 53"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       9.63194444
	Dec     -59.55
	Dist     2500
	Radius   13.53
	Age      131.8
}

Cluster	"NGC 2972"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       9.67
	Dec     -50.3233333
	Dist     2062
	Radius   0.8997
	Age      92.9
}

Cluster	"Ruprecht 79"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       9.68305556
	Dec     -53.85
	Dist     1979
	Radius   1.439
	Age      12.39
}

Cluster	"Ruprecht 80"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       9.70083333
	Dec     -44.0166667
	Dist     1428
	Radius   2.492
}

Cluster	"ASCC 54"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       9.74611111
	Dec     -54.44
	Dist     1200
	Radius   4.608
	Age      691.8
}

Cluster	"Ruprecht 82"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       9.76277778
	Dec     -54
	Dist     2455
	Radius   1.785
	Age      316.2
}

Cluster	"NGC 3033"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       9.81027778
	Dec     -56.4216667
	Dist     922
	Radius   0.5364
	Age      69.98
}

Cluster	"Ruprecht 84"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       9.81888889
	Dec     -65.25
	Dist     2500
	Radius   1.454
}

Cluster	"Ruprecht 83"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       9.82083333
	Dec     -54.6
	Dist     2459
	Radius   1.073
	Age      281.8
}

Cluster	"NGC 3036"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       9.82111111
	Dec     -62.6716667
	Dist     1200
	Radius   0.6981
	Age      407.4
}

Cluster	"Pismis 16"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       9.85444444
	Dec     -53.1666667
	Dist     1824
	Radius   0.5306
	Age      69.02
}

Cluster	"ASCC 55"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       9.905
	Dec     -57.08
	Dist     1100
	Radius   4.416
	Age      281.8
}

Cluster	"Collinder 213"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       9.91194444
	Dec     -50.9166667
	Dist     1400
	Radius   3.462
	Age      691.8
}

Cluster	"NGC 3105"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       10.0108333
	Dec     -54.7883333
	Dist     8530
	Radius   2.481
	Age      19.95
}

Cluster	"Ruprecht 85"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       10.0238889
	Dec     -55.1166667
	Dist     833
	Radius   0.3635
}

Cluster	"NGC 3114"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       10.0433333
	Dec     -60.12
	Dist     911
	Radius   4.638
	Age      123.9
}

Cluster	"Loden 28"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       10.0533333
	Dec     -58.15
	Dist     3950
	Radius   11.49
	Age      19.95
}

Cluster	"Trumpler 11"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       10.0816667
	Dec     -61.6
	Dist     3100
	Radius   2.254
}

Cluster	"Loden 1"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       10.0841667
	Dec     -55.8
	Dist     360
	Radius   1.1
	Age      1950
}

Cluster	"Trumpler 12"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       10.1080556
	Dec     -60.3
	Dist     1249
	Radius   0.7266
}

Cluster	"Hogg 6"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       10.1102778
	Dec     -60.5
	Dist     2000
	Radius   0.8727
}

Cluster	"ASCC 56"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       10.1369444
	Dec     -64.37
	Dist     800
	Radius   4.887
	Age      120.2
}

Cluster	"Ruprecht 161"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       10.1461111
	Dec     -61.25
	Dist     1428
	Radius   6.646
}

Cluster	"ASCC 57"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       10.18
	Dec     -66.68
	Dist     1500
	Radius   9.949
	Age      501.2
}

Cluster	"BH 90"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       10.2027778
	Dec     -58.0666667
	Dist     2572
	Radius   1.496
	Age      87.7
}

Cluster	"ESO 092-18"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       10.2494444
	Dec     -64.6116667
	Dist     10607
	Radius   7.714
	Age      1057
}

Cluster	"ASCC 58"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       10.2519444
	Dec     -54.97
	Dist     600
	Radius   4.189
	Age      10.96
}

Cluster	"BH 91"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       10.2883333
	Dec     -58.7
	Dist     909
	Radius   0.661
}

Cluster	"BH 92"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       10.3186111
	Dec     -56.4166667
	Dist     1249
	Radius   0.3633
}

Cluster	"ASCC 59"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       10.3369444
	Dec     -57.65
	Dist     550
	Radius   3.36
	Age      398.1
}

Cluster	"NGC 3228"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       10.3561111
	Dec     -51.7283333
	Dist     544
	Radius   0.3956
	Age      85.51
}

Cluster	"Loden 46"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       10.3833333
	Dec     -54.8
	Dist     540
	Radius   2.985
	Age      1072
}

Cluster	"Trumpler 13"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       10.3966667
	Dec     -60.1333333
	Dist     2400
	Radius   1.745
	Age      316.2
}

Cluster	"Westerlund 2"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       10.4005556
	Dec     -57.7666667
	Dist     6400
	Radius   1.862
	Age      1.995
}

Cluster	"Collinder 220"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       10.4311111
	Dec     -57.9266667
	Dist     1547
	Radius   2.138
	Age      121.1
}

Cluster	"NGC 3255"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       10.4419444
	Dec     -60.6766667
	Dist     1445
	Radius   0.4203
	Age      199.5
}

Cluster	"IC 2581"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       10.4580556
	Dec     -57.6166667
	Dist     2446
	Radius   1.779
	Age      13.87
}

Cluster	"Ruprecht 89"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       10.4738889
	Dec     -58.1833333
	Dist     909
	Radius   0.5288
}

Cluster	"Loden 143"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       10.4816667
	Dec     -58.7833333
	Dist     400
	Radius   2.385
}

Cluster	"Loden 89"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       10.4866667
	Dec     -56.7833333
	Dist     380
	Radius   1.934
	Age      295.1
}

Cluster	"Loden 59"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       10.505
	Dec     -54.1333333
	Dist     1111
	Radius   0.3232
}

Cluster	"Ruprecht 90"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       10.5166667
	Dec     -58.45
	Dist     1000
	Radius   0.7272
}

Cluster	"Collinder 223"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       10.5377778
	Dec     -60.02
	Dist     2820
	Radius   7.383
	Age      100
}

Cluster	"Loden 112"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       10.54
	Dec     -56.7
	Dist     2500
	Radius   2.545
	Age      9.12
}

Cluster	"ASCC 60"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       10.5519444
	Dec     -58.48
	Dist     800
	Radius   1.676
	Age      229.1
}

Cluster	"Loden 153"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       10.5783333
	Dec     -58.1333333
	Dist     2670
	Radius   0.7767
	Age      5.495
}

Cluster	"Bochum 9"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       10.5961111
	Dec     -60.1166667
	Dist     4600
	Radius   10.04
}

Cluster	"NGC 3293"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       10.5975
	Dec     -58.23
	Dist     2327
	Radius   2.031
	Age      10.33
}

Cluster	"Loden 165"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       10.5988889
	Dec     -58.7366667
	Dist     1900
	Radius   3.04
	Age      3020
}

Cluster	"Carraro 1"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       10.6166667
	Dec     -58.7333333
	Dist     1900
	Radius   1.105
	Age      3020
}

Cluster	"NGC 3324"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       10.6222222
	Dec     -58.6416667
	Dist     2317
	Radius   4.044
	Age      5.675
}

Cluster	"BH 99"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       10.6316667
	Dec     -59.1833333
	Dist     507
	Radius   1.475
	Age      40.27
}

Cluster	"NGC 3330"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       10.6461111
	Dec     -54.1233333
	Dist     894
	Radius   0.5201
	Age      169.4
}

Cluster	"ESO 062-11"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       10.6486111
	Dec     -69.0333333
	Dist     910
	Radius   0.7941
	Age      338.8
}

Cluster	"Saurer 3"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       10.6902778
	Dec     -55.3055556
	Dist     9550
	Radius   5.556
	Age      1995
}

Cluster	"Bochum 10"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       10.7033333
	Dec     -59.1333333
	Dist     2027
	Radius   5.896
	Age      7.194
}

Cluster	"Melotte 101"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       10.7033333
	Dec     -65.1
	Dist     1995
	Radius   4.352
	Age      77.62
}

Cluster	"Alessi 5"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       10.7188889
	Dec     -61.1666667
	Dist     398
	Radius   1.91
	Age      39.81
}

Cluster	"Trumpler 14"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       10.7322222
	Dec     -59.55
	Dist     2500
	Radius   1.818
	Age      1.995
}

Cluster	"Collinder 228"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       10.7333333
	Dec     -60.0866667
	Dist     2201
	Radius   4.482
	Age      6.761
}

Cluster	"Collinder 232"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       10.7441667
	Dec     -59.56
	Dist     2300
	Radius   1.338
	Age      1.995
}

Cluster	"Trumpler 15"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       10.7452778
	Dec     -59.3666667
	Dist     1853
	Radius   3.773
	Age      8.433
}

Cluster	"Trumpler 16"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       10.7527778
	Dec     -59.7166667
	Dist     3900
	Radius   5.672
	Age      5.012
}

Cluster	"ASCC 61"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       10.7688889
	Dec     -56.86
	Dist     1700
	Radius   9.495
	Age      91.2
}

Cluster	"Bochum 11"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       10.7875
	Dec     -60.0833333
	Dist     2412
	Radius   7.367
	Age      5.808
}

Cluster	"Ruprecht 91"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       10.7938889
	Dec     -57.4672222
	Dist     769
	Radius   1.901
}

Cluster	"Loden 189"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       10.8333333
	Dec     -56.4
	Dist     720
	Radius   2.304
	Age      436.5
}

Cluster	"ASCC 62"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       10.8480556
	Dec     -60.1
	Dist     3000
	Radius   14.66
	Age      31.62
}

Cluster	"ESO 128-16"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       10.8947222
	Dec     -58.24
	Dist     900
	Radius   1.309
	Age      831.8
}

Cluster	"Ruprecht 92"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       10.8963889
	Dec     -61.75
	Dist     2362
	Radius   2.405
	Age      62.81
}

Cluster	"Kronberger 39"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       10.9038889
	Dec     -61.7377778
	Dist     11100
	Radius   1.292
}

Cluster	"ASCC 63"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       10.9311111
	Dec     -60.41
	Dist     3500
	Radius   9.163
	Age      17.38
}

Cluster	"Graham 1"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       10.9422222
	Dec     -63.0177778
	Dist     3600
	Radius   1.047
}

Cluster	"Trumpler 17"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       10.94
	Dec     -59.2
	Dist     2189
	Radius   1.592
	Age      50.82
}

Cluster	"Collinder 236"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       10.9475
	Dec     -61.1166667
	Dist     769
	Radius   1.118
}

Cluster	"Bochum 12"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       10.9566667
	Dec     -61.7166667
	Dist     2218
	Radius   3.226
	Age      40.74
}

Cluster	"NGC 3496"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       10.9933333
	Dec     -60.3366667
	Dist     990
	Radius   1.152
	Age      295.8
}

Cluster	"Sher 1"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       11.0177778
	Dec     -60.2333333
	Dist     5875
	Radius   0.8545
	Age      5.164
}

Cluster	"Pismis 17"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       11.0183333
	Dec     -59.8166667
	Dist     3504
	Radius   3.058
	Age      10.54
}

Cluster	"ASCC 64"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       11.0511111
	Dec     -60.92
	Dist     1500
	Radius   4.712
	Age      83.18
}

Cluster	"Ruprecht 93"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       11.0733333
	Dec     -61.3683333
	Dist     1437
	Radius   1.463
	Age      156
}

Cluster	"NGC 3532"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       11.0941667
	Dec     -58.7533333
	Dist     486
	Radius   3.534
	Age      310.5
}

Cluster	"Feinstein 1"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       11.0988889
	Dec     -59.8166667
	Dist     1159
	Radius   4.214
	Age      10
}

Cluster	"Loden 306"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       11.1022222
	Dec     -61.1
	Dist     2000
	Radius   7.563
	Age      5.754
}

Cluster	"Shorlin 1"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       11.1052778
	Dec     -61.2383333
	Dist     12600
	Radius   1.466
}

Cluster	"BH 111"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       11.1522222
	Dec     -63.8333333
	Dist     667
	Radius   0.194
}

Cluster	"NGC 3572"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       11.1730556
	Dec     -60.2483333
	Dist     1995
	Radius   1.451
	Age      7.78
}

Cluster	"Basel 17"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       11.1761111
	Dec     -59.0333333
	Dist     1636
	Radius   2.379
}

Cluster	"Hogg 10"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       11.1783333
	Dec     -60.4
	Dist     1776
	Radius   0.7749
	Age      6.081
}

Cluster	"Loden 309"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       11.1786111
	Dec     -60.3833333
	Dist     1000
	Radius   0.2182
}

Cluster	"ASCC 65"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       11.185
	Dec     -61.12
	Dist     3500
	Radius   13.44
	Age      12.3
}

Cluster	"Trumpler 18"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       11.1911111
	Dec     -60.6666667
	Dist     1358
	Radius   0.9876
	Age      15.63
}

Cluster	"Hogg 11"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       11.1936111
	Dec     -60.4
	Dist     2270
	Radius   0.6603
	Age      12.02
}

Cluster	"Collinder 240"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       11.1944444
	Dec     -60.3097222
	Dist     1577
	Radius   7.34
	Age      14.45
}

Cluster	"ESO 570-12"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       11.2019444
	Dec     -21.35
	Dist     640
	Radius   0.7447
	Age      794.3
}

Cluster	"NGC 3590"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       11.2163889
	Dec     -60.7883333
	Dist     1651
	Radius   0.7204
	Age      17.02
}

Cluster	"Hogg 12"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       11.2169444
	Dec     -60.7833333
	Dist     1428
	Radius   0.8308
}

Cluster	"Stock 13"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       11.2180556
	Dec     -58.8833333
	Dist     1577
	Radius   1.147
	Age      16.67
}

Cluster	"ASCC 66"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       11.2269444
	Dec     -55.42
	Dist     1000
	Radius   5.236
	Age      57.54
}

Cluster	"NGC 3603"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       11.2519444
	Dec     -61.26
	Dist     6900
	Radius   4.014
	Age      1
}

Cluster	"IC 2714"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       11.2908333
	Dec     -62.7333333
	Dist     1238
	Radius   2.521
	Age      348.3
}

Cluster	"ESO 093-08"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       11.3280556
	Dec     -65.22
	Dist     14000
	Radius   2.036
	Age      5495
}

Cluster	"Melotte 105"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       11.3283333
	Dec     -63.4833333
	Dist     2208
	Radius   1.606
	Age      207
}

Cluster	"NGC 3680"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       11.4272222
	Dec     -43.2433333
	Dist     938
	Radius   0.6821
	Age      1194
}

Cluster	"Ruprecht 94"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       11.5102778
	Dec     -63.4333333
	Dist     1111
	Radius   3.232
}

Cluster	"Ruprecht 164"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       11.5141667
	Dec     -60.7333333
	Dist     1000
	Radius   0.7272
}

Cluster	"Loden 372"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       11.5152778
	Dec     -58.4833333
	Dist     1200
	Radius   1.047
	Age      354.8
}

Cluster	"Loden 402"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       11.5477778
	Dec     -60.7166667
	Dist     2450
	Radius   3.563
	Age      158.5
}

Cluster	"NGC 3766"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       11.6038889
	Dec     -61.6083333
	Dist     2218
	Radius   3
	Age      20.89
}

Cluster	"IC 2944"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       11.6388889
	Dec     -63.3727778
	Dist     1794
	Radius   16.96
	Age      6.577
}

Cluster	"ASCC 67"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       11.6919444
	Dec     -61.02
	Dist     1500
	Radius   5.236
	Age      46.77
}

Cluster	"Bica 5"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       11.6927778
	Dec     -62.4180556
	Dist     1740
	Radius   0.7592
	Age      602.6
}

Cluster	"Stock 14"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       11.73
	Dec     -62.5166667
	Dist     2146
	Radius   1.873
	Age      11.43
}

Cluster	"NGC 3960"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       11.8425
	Dec     -55.6733333
	Dist     1850
	Radius   1.345
	Age      1259
}

Cluster	"Ruprecht 96"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       11.8436111
	Dec     -62.1333333
	Dist     860
	Radius   0.6254
	Age      1000
}

Cluster	"Loden 481"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       11.8630556
	Dec     -61.2833333
	Dist     1520
	Radius   5.748
	Age      154.9
}

Cluster	"Loden 480"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       11.9333333
	Dec     -58.4333333
	Dist     1429
	Radius   0.5196
}

Cluster	"Ruprecht 97"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       11.9577778
	Dec     -62.7166667
	Dist     1357
	Radius   0.9868
	Age      220.3
}

Cluster	"Ruprecht 98"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       11.9777778
	Dec     -64.5833333
	Dist     494
	Radius   1.006
	Age      322.1
}

Cluster	"Feigelson 1"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       11.9975
	Dec     -78.2075
	Dist     114
	Radius   0.2985
	Age      3.999
}

Cluster	"NGC 4052"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       12.0172222
	Dec     -63.225
	Dist     1209
	Radius   1.583
	Age      312.6
}

Cluster	"Alessi-Teutsch 8"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       12.0486111
	Dec     -60.925
	Dist     650
	Radius   2.269
	Age      446.7
}

Cluster	"Ruprecht 99"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       12.0511111
	Dec     -63.85
	Dist     660
	Radius   0.48
	Age      1950
}

Cluster	"ASCC 69"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       12.11
	Dec     -69.77
	Dist     1000
	Radius   6.981
	Age      81.28
}

Cluster	"NGC 4103"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       12.1111111
	Dec     -61.25
	Dist     1632
	Radius   1.424
	Age      24.72
}

Cluster	"ESO 130-06"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       12.1294444
	Dec     -59.3
	Dist     2200
	Radius   3.2
	Age      213.8
}

Cluster	"Loden 565"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       12.1386111
	Dec     -60.65
	Dist     1111
	Radius   2.585
}

Cluster	"Saurer 4"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       12.2341667
	Dec     -63.5933333
	Dist     6190
	Radius   1.621
	Age      1514
}

Cluster	"ASCC 70"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       12.25
	Dec     -64.43
	Dist     2700
	Radius   14.14
	Age      8.318
}

Cluster	"Loden 615"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       12.3441667
	Dec     -64.7833333
	Dist     1667
	Radius   0.6061
}

Cluster	"ASCC 71"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       12.345
	Dec     -67.52
	Dist     1300
	Radius   9.303
	Age      75.86
}

Cluster	"NGC 4337"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       12.4011111
	Dec     -58.1233333
	Dist     489
	Radius   0.2845
	Age      284.4
}

Cluster	"NGC 4349"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       12.4022222
	Dec     -61.8716667
	Dist     2176
	Radius   1.582
	Age      206.5
}

Cluster	"Melotte 111"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       12.4183333
	Dec      26.1
	Dist     96
	Radius   1.676
	Age      448.7
}

Cluster	"Harvard 5"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       12.4544444
	Dec     -60.7788889
	Dist     1184
	Radius   0.861
	Age      107.6
}

Cluster	"NGC 4439"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       12.4741667
	Dec     -60.105
	Dist     1785
	Radius   1.038
	Age      81.1
}

Cluster	"Hogg 23"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       12.4755556
	Dec     -60.8952778
	Dist     1250
	Radius   1.273
	Age      204.2
}

Cluster	"Hogg 14"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       12.4766667
	Dec     -59.8166667
	Dist     969
	Radius   0.4228
	Age      125.6
}

Cluster	"NGC 4463"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       12.4988889
	Dec     -64.79
	Dist     1050
	Radius   0.5345
	Age      31.99
}

Cluster	"ASCC 72"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       12.55
	Dec     -60.95
	Dist     1100
	Radius   4.8
	Age      134.9
}

Cluster	"Ruprecht 105"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       12.5705556
	Dec     -61.5666667
	Dist     950
	Radius   1.658
	Age      1023
}

Cluster	"ASCC 73"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       12.61
	Dec     -67.29
	Dist     650
	Radius   4.538
	Age      154.9
}

Cluster	"Collinder 261"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       12.6325
	Dec     -68.3666667
	Dist     2190
	Radius   2.867
	Age      8913
}

Cluster	"Trumpler 20"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       12.6586111
	Dec     -60.6333333
	Dist     3300
	Radius   7.679
	Age      1288
}

Cluster	"NGC 4609"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       12.705
	Dec     -62.995
	Dist     1223
	Radius   0.7115
	Age      77.98
}

Cluster	"Hogg 15"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       12.7269444
	Dec     -63.1
	Dist     2262
	Radius   0.658
	Age      5.984
}

Cluster	"Loden 682"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       12.7880556
	Dec     -60.65
	Dist     900
	Radius   3.403
	Age      257
}

Cluster	"Loden 694"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       12.8836111
	Dec     -60.7666667
	Dist     1700
	Radius   5.687
	Age      23.99
}

Cluster	"NGC 4755"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       12.8941667
	Dec     -60.3616667
	Dist     1976
	Radius   2.874
	Age      16.44
}

Cluster	"NGC 4815"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       12.9663889
	Dec     -64.96
	Dist     3079
	Radius   2.239
	Age      233.9
}

Cluster	"NGC 4852"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       13.0025
	Dec     -59.6133333
	Dist     1100
	Radius   1.6
	Age      199.5
}

Cluster	"NGC 5045"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       13.2361111
	Dec     -63.39
	Dist     1500
	Radius   9.861
	Age      12.88
}

Cluster	"BH 144"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       13.2516667
	Dec     -65.9166667
	Dist     12000
	Radius   2.618
	Age      794.3
}

Cluster	"NGC 5043"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       13.2691667
	Dec     -60.0733333
	Dist     970
	Radius   1.975
	Age      616.6
}

Cluster	"Collinder 268"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       13.3036111
	Dec     -67.0833333
	Dist     1963
	Radius   1.428
	Age      574.1
}

Cluster	"Stock 16"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       13.3247222
	Dec     -62.6333333
	Dist     1810
	Radius   0.7898
	Age      7.943
}

Cluster	"Ruprecht 107"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       13.3294444
	Dec     -64.95
	Dist     1442
	Radius   0.6292
	Age      30.06
}

Cluster	"Collinder 269"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       13.3919444
	Dec     -66.1833333
	Dist     1300
	Radius   2.836
	Age      331.1
}

Cluster	"Teutsch 79"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       13.3941667
	Dec     -63.6694444
	Dist     6700
	Radius   1.949
}

Cluster	"Loden 821"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       13.4058333
	Dec     -59.7333333
	Dist     2800
	Radius   7.738
	Age      19.5
}

Cluster	"Loden 807"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       13.4111111
	Dec     -62.4833333
	Dist     925
	Radius   2.691
	Age      199.5
}

Cluster	"NGC 5138"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       13.4544444
	Dec     -59.0333333
	Dist     1986
	Radius   2.022
	Age      96.83
}

Cluster	"Basel 18"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       13.4622222
	Dec     -62.3127778
	Dist     2226
	Radius   1.943
	Age      38.9
}

Cluster	"Hogg 16"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       13.4883333
	Dec     -61.2
	Dist     1585
	Radius   1.383
	Age      11.14
}

Cluster	"Collinder 271"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       13.4983333
	Dec     -64.2
	Dist     1169
	Radius   0.8501
	Age      209.9
}

Cluster	"Collinder 272"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       13.5072222
	Dec     -61.3166667
	Dist     2045
	Radius   2.974
	Age      16.87
}

Cluster	"NGC 5168"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       13.5183333
	Dec     -60.94
	Dist     1777
	Radius   1.034
	Age      100.2
}

Cluster	"ESO 383-10"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       13.525
	Dec     -35.0655556
	Dist     1000
	Radius   0.8727
	Age      1995
}

Cluster	"Ruprecht 108"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       13.5363889
	Dec     -58.4666667
	Dist     901
	Radius   1.31
	Age      265.5
}

Cluster	"Trumpler 21"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       13.5372222
	Dec     -62.8
	Dist     1263
	Radius   0.9185
	Age      49.66
}

Cluster	"Loden 915"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       13.5683333
	Dec     -59.25
	Dist     500
	Radius   2.327
	Age      275.4
}

Cluster	"C1331-622"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       13.57
	Dec     -62.4172222
	Dist     819
	Radius   0.8338
}

Cluster	"ASCC 74"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       13.5969444
	Dec     -58.81
	Dist     550
	Radius   1.92
	Age      316.2
}

Cluster	"ESO 132-14"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       13.6083333
	Dec     -62.2136111
	Dist     1100
	Radius   0.48
	Age      794.3
}

Cluster	"Pismis 18"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       13.6152778
	Dec     -62.0933333
	Dist     2240
	Radius   1.303
	Age      1202
}

Cluster	"BH 151"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       13.67
	Dec     -61.7291667
	Dist     3800
	Radius   0.829
}

Cluster	"Platais 10"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       13.7244444
	Dec     -59.1216667
	Dist     246
	Radius   11.6
	Age      100
}

Cluster	"Dias 4"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       13.7236111
	Dec     -63.0133333
	Dist     2150
	Radius   2.001
	Age      1259
}

Cluster	"Loden 1010"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       13.7466667
	Dec     -60.2833333
	Dist     1111
	Radius   5.009
}

Cluster	"NGC 5281"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       13.7763889
	Dec     -62.9166667
	Dist     1108
	Radius   1.128
	Age      14
}

Cluster	"ASCC 75"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       13.7861111
	Dec     -62.42
	Dist     3000
	Radius   8.901
	Age      4.467
}

Cluster	"Platais 11"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       13.7991667
	Dec     -66.02
	Dist     232
	Radius   6.075
	Age      199.5
	MaxStarAppMagn 7.0
}

Cluster	"Platais 12"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       13.8622222
	Dec     -63.4533333
	Dist     402
	Radius   7.017
	Age      199.5
}

Cluster	"Loden 995"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       13.8655556
	Dec     -64.875
	Dist     2400
	Radius   9.25
	Age      218.8
}

Cluster	"ASCC 76"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       13.8711111
	Dec     -66.4
	Dist     600
	Radius   3.665
	Age      28.18
}

Cluster	"NGC 5316"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       13.8991667
	Dec     -61.8683333
	Dist     1215
	Radius   2.474
	Age      159.2
}

Cluster	"Loden 1171"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       13.99
	Dec     -58.3833333
	Dist     714
	Radius   1.142
}

Cluster	"Lynga 1"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       14.0005556
	Dec     -62.15
	Dist     1900
	Radius   0.829
	Age      100
}

Cluster	"NGC 5359"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       14.0025
	Dec     -70.3916667
	Dist     2500
	Radius   5.818
	Age      199.5
}

Cluster	"Ruprecht 110"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       14.0905556
	Dec     -67.4666667
	Dist     800
	Radius   2.094
}

Cluster	"Loden 1194"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       14.095
	Dec     -59.7
	Dist     500
	Radius   1.018
	Age      338.8
}

Cluster	"NGC 5460"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       14.1241667
	Dec     -48.3433333
	Dist     678
	Radius   3.451
	Age      161.1
}

Cluster	"Loden 1225"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       14.1508333
	Dec     -59.7166667
	Dist     1429
	Radius   0.8314
}

Cluster	"ASCC 77"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       14.18
	Dec     -62.33
	Dist     2200
	Radius   12.29
	Age      9.772
}

Cluster	"Ruprecht 167"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       14.3030556
	Dec     -58.9666667
	Dist     1250
	Radius   2.545
}

Cluster	"Loden 1256"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       14.3033333
	Dec     -61.4333333
	Dist     1250
	Radius   1.818
}

Cluster	"ESO 175-06"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       14.3088889
	Dec     -56.9183333
	Dist     550
	Radius   1.44
	Age      398.1
}

Cluster	"Lynga 2"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       14.4097222
	Dec     -61.3305556
	Dist     900
	Radius   1.702
	Age      89.13
}

Cluster	"NGC 5593"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       14.4275
	Dec     -54.7983333
	Dist     1428
	Radius   2.077
}

Cluster	"NGC 5606"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       14.4630556
	Dec     -59.6316667
	Dist     1805
	Radius   0.7876
	Age      11.89
}

Cluster	"NGC 5617"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       14.4955556
	Dec     -60.7116667
	Dist     2000
	Radius   2.909
	Age      79.43
}

Cluster	"Pismis 19"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       14.5111111
	Dec     -60.8833333
	Dist     1500
	Radius   0.6545
	Age      794.3
}

Cluster	"Trumpler 22"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       14.5172222
	Dec     -61.1666667
	Dist     1516
	Radius   2.205
	Age      89.13
}

Cluster	"Hogg 17"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       14.5661111
	Dec     -61.3666667
	Dist     1310
	Radius   0.7621
	Age      107.2
}

Cluster	"NGC 5662"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       14.5936111
	Dec     -56.6183333
	Dist     666
	Radius   2.809
	Age      92.9
}

Cluster	"Collinder 285"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       14.685
	Dec      69.5666667
	Dist     25
	Radius   5.162
	Age      199.5
}

Cluster	"NGC 5715"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       14.725
	Dec     -57.5769444
	Dist     1500
	Radius   1.309
	Age      794.3
}

Cluster	"BH 164"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       14.8038889
	Dec     -66.3366667
	Dist     541
	Radius   4.721
}

Cluster	"NGC 5749"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       14.8147222
	Dec     -54.4983333
	Dist     1031
	Radius   1.5
	Age      53.46
}

Cluster	"Hogg 18"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       14.8452778
	Dec     -52.2666667
	Dist     1535
	Radius   1.116
	Age      57.41
}

Cluster	"Teutsch 80"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       14.8905556
	Dec     -60.4825
	Dist     2500
	Radius   1.236
	Age      125.9
}

Cluster	"NGC 5764"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       14.8922222
	Dec     -52.67
	Dist     2800
	Radius   1.222
	Age      199.5
}

Cluster	"NGC 5822"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       15.0725
	Dec     -54.3966667
	Dist     917
	Radius   4.668
	Age      662.2
}

Cluster	"ASCC 78"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       15.085
	Dec     -68.39
	Dist     2400
	Radius   6.702
	Age      302
}

Cluster	"NGC 5823"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       15.0916667
	Dec     -55.6033333
	Dist     1192
	Radius   2.08
	Age      794.3
}

Cluster	"Pismis 20"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       15.2563889
	Dec     -59.0666667
	Dist     2018
	Radius   1.174
	Age      7.311
}

Cluster	"ASCC 79"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       15.32
	Dec     -60.73
	Dist     800
	Radius   7.261
	Age      7.244
}

Cluster	"ASCC 80"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       15.41
	Dec     -60.14
	Dist     1500
	Radius   6.545
	Age      85.11
}

Cluster	"Alessi 8"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       15.4925
	Dec     -51.2333333
	Dist     575
	Radius   2.007
	Age      141.3
}

Cluster	"Lynga 4"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       15.5552778
	Dec     -55.2363889
	Dist     1100
	Radius   0.9599
	Age      1288
}

Cluster	"Loden 2313"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       15.6802778
	Dec     -52.4333333
	Dist     1410
	Radius   4.307
	Age      549.5
}

Cluster	"Loden 2326"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       15.7258333
	Dec     -52.475
	Dist     900
	Radius   0.2094
	Age      199.5
}

Cluster	"Johansson 1"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       15.7722222
	Dec     -52.3816667
	Dist     570
	Radius   2.404
	Age      199.5
}

Cluster	"ASCC 81"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       15.7819444
	Dec     -50.98
	Dist     700
	Radius   3.177
	Age      239.9
}

Cluster	"ASCC 82"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       15.79
	Dec     -64.41
	Dist     800
	Radius   4.189
	Age      467.7
}

Cluster	"Collinder 292"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       15.8355556
	Dec     -57.6166667
	Dist     1667
	Radius   3.394
}

Cluster	"ASCC 83"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       15.8369444
	Dec     -52.8
	Dist     600
	Radius   2.199
	Age      251.2
}

Cluster	"NGC 5999"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       15.8688889
	Dec     -56.4733333
	Dist     2050
	Radius   0.8945
	Age      398.1
}

Cluster	"ASCC 84"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       15.915
	Dec     -60.74
	Dist     900
	Radius   3.927
	Age      47.86
}

Cluster	"NGC 6005"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       15.93
	Dec     -57.4366667
	Dist     2690
	Radius   1.956
	Age      1202
}

Cluster	"Ruprecht 113"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       15.9508333
	Dec     -59.4666667
	Dist     667
	Radius   2.425
}

Cluster	"Trumpler 23"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       16.0136111
	Dec     -53.5361111
	Dist     1900
	Radius   1.382
	Age      891.3
}

Cluster	"Moffat 1"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       16.025
	Dec     -54.1166667
	Dist     2100
	Radius   2.138
	Age      10
}

Cluster	"NGC 6025"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       16.0547222
	Dec     -60.4316667
	Dist     756
	Radius   1.539
	Age      77.45
}

Cluster	"Lynga 6"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       16.0811111
	Dec     -51.9333333
	Dist     1600
	Radius   1.164
	Age      26.92
}

Cluster	"NGC 6031"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       16.1263889
	Dec     -54.015
	Dist     1823
	Radius   0.7954
	Age      117.2
}

Cluster	"Ruprecht 115"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       16.2144444
	Dec     -52.4
	Dist     2160
	Radius   1.571
	Age      602.6
}

Cluster	"NGC 6067"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       16.2197222
	Dec     -54.2183333
	Dist     1417
	Radius   2.885
	Age      119.1
}

Cluster	"Pismis 22"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       16.2358333
	Dec     -51.8666667
	Dist     1000
	Radius   0.5818
	Age      39.81
}

Cluster	"Harvard 10"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       16.3133333
	Dec     -54.9333333
	Dist     1312
	Radius   4.771
	Age      218.8
}

Cluster	"NGC 6087"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       16.3138889
	Dec     -57.935
	Dist     891
	Radius   1.814
	Age      94.62
}

Cluster	"Lynga 8"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       16.3344444
	Dec     -50.2330556
	Dist     1050
	Radius   0.4581
	Age      1995
}

Cluster	"Lynga 9"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       16.3447222
	Dec     -48.5288889
	Dist     1700
	Radius   1.484
	Age      707.9
}

Cluster	"Ruprecht 116"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       16.3888889
	Dec     -52
	Dist     667
	Radius   0.4851
}

Cluster	"Pismis 23"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       16.3994444
	Dec     -48.8925
	Dist     2600
	Radius   0.3782
	Age      302
}

Cluster	"Ruprecht 118"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       16.41
	Dec     -51.9666667
	Dist     1343
	Radius   0.586
	Age      128.8
}

Cluster	"NGC 6124"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       16.4222222
	Dec     -40.6533333
	Dist     512
	Radius   2.904
	Age      140.3
}

Cluster	"NGC 6134"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       16.4627778
	Dec     -49.1516667
	Dist     913
	Radius   0.7967
	Age      929
}

Cluster	"Ruprecht 119"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       16.4708333
	Dec     -51.5
	Dist     956
	Radius   1.112
	Age      7.129
}

Cluster	"NGC 6152"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       16.5458333
	Dec     -52.6433333
	Dist     1030
	Radius   3.745
}

Cluster	"NGC 6167"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       16.5761111
	Dec     -49.7716667
	Dist     1108
	Radius   1.128
	Age      77.09
}

Cluster	"Ruprecht 120"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       16.5861111
	Dec     -48.2833333
	Dist     2000
	Radius   0.8727
	Age      151.4
}

Cluster	"NGC 6178"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       16.5963889
	Dec     -45.6433333
	Dist     1014
	Radius   0.7374
	Age      17.7
}

Cluster	"NGC 6192"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       16.6730556
	Dec     -43.3666667
	Dist     1547
	Radius   2.025
	Age      134.9
}

Cluster	"NGC 6193"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       16.6888889
	Dec     -48.7633333
	Dist     1155
	Radius   2.352
	Age      5.957
}

Cluster	"NGC 6200"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       16.7352778
	Dec     -47.4633333
	Dist     2054
	Radius   4.182
	Age      8.472
}

Cluster	"Lynga 12"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       16.7677778
	Dec     -50.7638889
	Dist     1000
	Radius   1.164
	Age      562.3
}

Cluster	"NGC 6204"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       16.7691667
	Dec     -47.0166667
	Dist     1200
	Radius   0.8727
	Age      79.43
}

Cluster	"Hogg 22"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       16.7769444
	Dec     -47.0833333
	Dist     1216
	Radius   0.5306
	Age      6.026
}

Cluster	"Westerlund 1"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       16.7844444
	Dec     -45.8433333
	Dist     5500
	Radius   1.92
	Age      5.012
}

Cluster	"ASCC 85"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       16.7919444
	Dec     -45.46
	Dist     1200
	Radius   4.608
	Age      26.3
}

Cluster	"NGC 6216"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       16.8233333
	Dec     -44.7283333
	Dist     4300
	Radius   2.502
	Age      34.67
}

Cluster	"NGC 6208"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       16.8244444
	Dec     -53.7283333
	Dist     939
	Radius   2.458
	Age      1172
}

Cluster	"NGC 6231"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       16.9027778
	Dec     -41.825
	Dist     1243
	Radius   2.531
	Age      6.966
}

Cluster	"ESO 332-08"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       16.9119444
	Dec     -40.7083333
	Dist     1200
	Radius   1.745
	Age      147.9
}

Cluster	"Lynga 14"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       16.9177778
	Dec     -45.2333333
	Dist     881
	Radius   0.3844
	Age      5.152
}

Cluster	"Collinder 316"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       16.925
	Dec     -40.8333333
	Dist     1000
	Radius   14.55
}

Cluster	"NGC 6242"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       16.9258333
	Dec     -39.4616667
	Dist     1131
	Radius   1.48
	Age      40.55
}

Cluster	"BH 205"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       16.9363889
	Dec     -40.6666667
	Dist     417
	Radius   0.2426
}

Cluster	"Trumpler 24"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       16.95
	Dec     -40.6666667
	Dist     1138
	Radius   9.931
	Age      8.299
}

Cluster	"NGC 6249"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       16.9613889
	Dec     -44.8116667
	Dist     981
	Radius   0.7134
	Age      24.32
}

Cluster	"NGC 6250"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       16.9655556
	Dec     -45.9366667
	Dist     865
	Radius   1.258
	Age      26
}

Cluster	"NGC 6253"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       16.9847222
	Dec     -52.7083333
	Dist     1510
	Radius   0.8785
	Age      5012
}

Cluster	"NGC 6259"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       17.0125
	Dec     -44.655
	Dist     1031
	Radius   2.099
	Age      216.8
}

Cluster	"Alessi-Teutsch 12"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       17.0297222
	Dec     -58.9583333
	Dist     700
	Radius   6.72
	Age      11.75
}

Cluster	"NGC 6268"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       17.0361111
	Dec     -39.7283333
	Dist     1080
	Radius   0.9425
	Age      39.81
}

Cluster	"ASCC 87"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       17.05
	Dec     -28.45
	Dist     900
	Radius   8.64
	Age      331.1
}

Cluster	"Teutsch 84"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       17.0722222
	Dec     -42.0733333
	Dist     2200
	Radius   1.28
	Age      1000
}

Cluster	"NGC 6281"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       17.0780556
	Dec     -37.985
	Dist     479
	Radius   0.5573
	Age      314.1
}

Cluster	"ASCC 88"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       17.1130556
	Dec     -35.6
	Dist     1900
	Radius   18.24
	Age      14.79
}

Cluster	"NGC 6318"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       17.2697222
	Dec     -39.425
	Dist     2100
	Radius   1.222
	Age      158.5
}

Cluster	"Bochum 13"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       17.29
	Dec     -35.55
	Dist     1077
	Radius   2.193
	Age      6.653
}

Cluster	"NGC 6322"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       17.3069444
	Dec     -42.9333333
	Dist     996
	Radius   0.7243
	Age      11.43
}

Cluster	"BH 221"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       17.3108333
	Dec     -32.3166667
	Dist     833
	Radius   1.212
}

Cluster	"BH 222"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       17.3130556
	Dec     -38.2833333
	Dist     6000
	Radius   1.745
	Age      60.26
}

Cluster	"Havlen-Moffat 1"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       17.315
	Dec     -38.8166667
	Dist     3300
	Radius   2.4
	Age      3.981
}

Cluster	"Ruprecht 123"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       17.3905556
	Dec     -37.9
	Dist     714
	Radius   1.246
}

Cluster	"Alessi 24"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       17.4002778
	Dec     -62.8633333
	Dist     500
	Radius   9.6
	Age      10.72
}

Cluster	"Pismis 24"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       17.4119444
	Dec     -34.2063889
	Dist     1995
	Radius   0.5803
	Age      10
}

Cluster	"IC 4651"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       17.4136111
	Dec     -49.9333333
	Dist     888
	Radius   1.292
	Age      1140
}

Cluster	"Trumpler 26"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       17.4755556
	Dec     -29.4972222
	Dist     1000
	Radius   0.7272
	Age      707.9
}

Cluster	"Antalova 1"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       17.4816667
	Dec     -31.55
	Dist     1250
	Radius   6.363
}

Cluster	"Ruprecht 125"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       17.4938889
	Dec     -40.4666667
	Dist     769
	Radius   2.237
}

Cluster	"Collinder 333"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       17.5252778
	Dec     -34.0166667
	Dist     855
	Radius   0.8705
	Age      794.3
}

Cluster	"NGC 6383"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       17.58
	Dec     -32.5666667
	Dist     985
	Radius   2.865
	Age      9.162
}

Cluster	"Trumpler 27"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       17.6055556
	Dec     -33.5166667
	Dist     1211
	Radius   1.057
	Age      11.56
}

Cluster	"Trumpler 28"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       17.6166667
	Dec     -32.4833333
	Dist     1343
	Radius   0.9767
	Age      19.5
}

Cluster	"NGC 6396"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       17.6266667
	Dec     -35.0266667
	Dist     1192
	Radius   0.5201
	Age      32.06
}

Cluster	"ESO 139-13"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       17.6280556
	Dec     -58.13
	Dist     1500
	Radius   1.091
	Age      602.6
}

Cluster	"Ruprecht 127"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       17.6308333
	Dec     -36.3
	Dist     1466
	Radius   1.066
	Age      22.44
}

Cluster	"Mamajek 2"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       17.6333333
	Dec     -8.11166667
	Dist     161
	Radius   0.2342
	Age      125.9
}

Cluster	"Collinder 338"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       17.6391667
	Dec     -37.7166667
	Dist     500
	Radius   1.454
}

Cluster	"ASCC 90"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       17.6519444
	Dec     -34.8
	Dist     500
	Radius   3.142
	Age      645.7
}

Cluster	"NGC 6404"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       17.6602778
	Dec     -33.2466667
	Dist     2400
	Radius   1.745
	Age      501.2
}

Cluster	"NGC 6400"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       17.67
	Dec     -36.9483333
	Dist     1000
	Radius   1.745
}

Cluster	"M 6/NGC 6405"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       17.6722222
	Dec     -32.2533333
	Dist     487
	Radius   1.417
	Age      94.19
}

Cluster	"Trumpler 29"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       17.6919444
	Dec     -40.15
	Dist     500
	Radius   0.8727
}

Cluster	"Alessi 9"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       17.725
	Dec     -46.9666667
	Dist     211
	Radius   4.604
}

Cluster	"NGC 6416"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       17.7386111
	Dec     -32.3616667
	Dist     741
	Radius   1.509
	Age      122.2
}

Cluster	"BH 245"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       17.7711111
	Dec     -29.7
	Dist     1000
	Radius   0.1454
	Age      14.79
}

Cluster	"IC 4665"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       17.7716667
	Dec      5.71666667
	Dist     352
	Radius   3.584
	Age      43.05
}

Cluster	"Collinder 347"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       17.7716667
	Dec     -29.3333333
	Dist     1514
	Radius   2.202
	Age      12.02
}

Cluster	"NGC 6425"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       17.7836111
	Dec     -31.53
	Dist     778
	Radius   1.132
	Age      22.23
}

Cluster	"Ruprecht 130"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       17.7922222
	Dec     -30.1
	Dist     2100
	Radius   0.9163
	Age      50.12
}

Cluster	"Collinder 350"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       17.8016667
	Dec      1.3
	Dist     286
	Radius   1.622
}

Cluster	"ASCC 91"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       17.815
	Dec     -37.36
	Dist     800
	Radius   4.189
	Age      446.7
}

Cluster	"Ruprecht 131"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       17.8211111
	Dec     -29.25
	Dist     600
	Radius   0.6109
	Age      1479
}

Cluster	"NGC 6444"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       17.8263889
	Dec     -34.82
	Dist     1000
	Radius   0.4363
}

Cluster	"Collinder 351"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       17.8166667
	Dec     -28.7358333
	Dist     1310
	Radius   1.6
	Age      158.5
}

Cluster	"NGC 6451"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       17.8447222
	Dec     -30.21
	Dist     2080
	Radius   2.118
	Age      136.1
}

Cluster	"Alessi 31"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       17.845
	Dec     -12.0016667
	Dist     650
	Radius   3.403
	Age      1023
}

Cluster	"Basel 5"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       17.8741667
	Dec     -30.1
	Dist     766
	Radius   0.5571
	Age      741.3
}

Cluster	"NGC 6481"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       17.88
	Dec      4.16666667
	Dist     1180
	Radius   0.5149
	Age      3467
}

Cluster	"NGC 6469"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       17.8866667
	Dec     -22.275
	Dist     1100
	Radius   1.12
	Age      251.2
}

Cluster	"Czernik 37"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       17.8880556
	Dec     -27.3694444
	Dist     1700
	Radius   1.236
	Age      602.6
}

Cluster	"M 7/NGC 6475"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       17.8975
	Dec     -34.7933333
	Dist     301
	Radius   3.502
	Age      298.5
}

Cluster	"Trumpler 30"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       17.9461111
	Dec     -35.2666667
	Dist     625
	Radius   0.6363
}

Cluster	"M 23/NGC 6494"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       17.9511111
	Dec     -18.985
	Dist     628
	Radius   2.649
	Age      299.9
}

Cluster	"Ruprecht 135"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       17.97
	Dec     -11.65
	Dist     1850
	Radius   1.614
	Age      501.2
}

Cluster	"Ruprecht 169"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       17.9894444
	Dec     -24.7669444
	Dist     1390
	Radius   1.051
	Age      1000
}

Cluster	"Trumpler 31"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       17.9969444
	Dec     -28.1666667
	Dist     986
	Radius   0.717
	Age      741.3
}

Cluster	"NGC 6507"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       17.9972222
	Dec     -17.4505556
	Dist     1230
	Radius   2.361
	Age      398.1
}

Cluster	"Ruprecht 138"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       17.9988889
	Dec     -24.6825
	Dist     930
	Radius   0.8116
	Age      1995
}

Cluster	"Ruprecht 137"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.0044444
	Dec     -25.2275
	Dist     1450
	Radius   1.181
	Age      794.3
}

Cluster	"Ruprecht 139"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.0175
	Dec     -23.5333333
	Dist     833
	Radius   1.454
}

Cluster	"Collinder 359"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.0183333
	Dec      2.9
	Dist     249
	Radius   8.695
	Age      32.06
}

Cluster	"Bochum 14"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.0333333
	Dec     -23.6833333
	Dist     578
	Radius   0.1681
	Age      9.908
}

Cluster	"NGC 6514"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.045
	Dec     -22.9716667
	Dist     816
	Radius   3.323
	Age      23.33
}

Cluster	"NGC 6520"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.0566667
	Dec     -27.8883333
	Dist     1900
	Radius   0.5527
	Age      151.4
}

Cluster	"M 21/NGC 6531"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.0702778
	Dec     -22.49
	Dist     1205
	Radius   2.454
	Age      11.75
}

Cluster	"NGC 6530"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.0752778
	Dec     -24.3583333
	Dist     1330
	Radius   2.708
	Age      7.362
}

Cluster	"NGC 6546"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.1227778
	Dec     -23.2966667
	Dist     938
	Radius   1.91
	Age      70.63
}

Cluster	"ASCC 93"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.1369444
	Dec     -22.26
	Dist     2500
	Radius   11.78
	Age      16.6
}

Cluster	"vdBergh 113"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.1433333
	Dec     -21.4166667
	Dist     1667
	Radius   3.394
}

Cluster	"ESO 521-38"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.1569444
	Dec     -24.53
	Dist     1800
	Radius   0.7854
	Age      125.9
}

Cluster	"Collinder 367"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.1641667
	Dec     -23.8283333
	Dist     1250
	Radius   7.272
}

Cluster	"NGC 6561"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.175
	Dec     -16.725
	Dist     3400
	Radius   7.418
	Age      8.318
}

Cluster	"NGC 6568"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.2122222
	Dec     -21.605
	Dist     769
	Radius   1.342
}

Cluster	"ESO 522-05"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.2147222
	Dec     -24.3638889
	Dist     660
	Radius   0.4224
	Age      3162
}

Cluster	"Markarian 38"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.2547222
	Dec     -19
	Dist     1471
	Radius   0.4279
	Age      7.621
}

Cluster	"ASCC 94"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.26
	Dec     -14.99
	Dist     850
	Radius   3.709
	Age      602.6
}

Cluster	"NGC 6583"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.2636111
	Dec     -22.1366667
	Dist     2040
	Radius   1.484
	Age      1000
}

Cluster	"ASCC 95"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.2680556
	Dec     -25.71
	Dist     1500
	Radius   8.378
	Age      109.6
}

Cluster	"Collinder 469"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.2761111
	Dec     -18.3116667
	Dist     1481
	Radius   0.6462
	Age      62.95
}

Cluster	"Turner 4"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.2855556
	Dec     -18.7
	Dist     2330
	Radius   1.186
	Age      10
}

Cluster	"Turner 2"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.2863889
	Dec     -18.8241667
	Dist     1190
	Radius   1.315
	Age      100
}

Cluster	"Trumpler 32"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.2916667
	Dec     -13.35
	Dist     1720
	Radius   1.251
	Age      302
}

Cluster	"NGC 6596"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.2925
	Dec     -16.65
	Dist     1249
	Radius   1.817
}

Cluster	"Turner 3"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.2927778
	Dec     -18.8638889
	Dist     1790
	Radius   0.5207
	Age      28.84
}

Cluster	"Dias 5"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.2944444
	Dec     -19.67
	Dist     1760
	Radius   0.7679
	Age      13.8
}

Cluster	"NGC 6604"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.3008333
	Dec     -12.2416667
	Dist     1696
	Radius   1.233
	Age      6.457
}

Cluster	"NGC 6603"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.3072222
	Dec     -18.4066667
	Dist     3600
	Radius   3.142
	Age      199.5
}

Cluster	"Alessi 19"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.3077778
	Dec      12.1666667
	Dist     550
	Radius   7.04
	Age      100
}

Cluster	"NGC 6611"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.3133333
	Dec     -13.8066667
	Dist     1800
	Radius   1.571
	Age      1.288
}

Cluster	"M 18/NGC 6613"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.3327778
	Dec     -17.1016667
	Dist     1296
	Radius   0.9425
	Age      16.71
}

Cluster	"Ferrero 1"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.3369444
	Dec     -32.3516667
	Dist     750
	Radius   3.273
	Age      186.2
}

Cluster	"NGC 6618"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.3463889
	Dec     -16.1716667
	Dist     1300
	Radius   4.727
	Age      1
}

Cluster	"Kharchenko 2"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.3713889
	Dec     -14.59
	Dist     1990
	Radius   0.8104
	Age      100
}

Cluster	"Kharchenko 3"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.3797222
	Dec     -14.6333333
	Dist     2130
	Radius   2.478
	Age      100
}

Cluster	"NGC 6625"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.3805556
	Dec     -11.9616667
	Dist     1335
	Radius   2.99
	Age      501.2
}

Cluster	"Trumpler 33"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.4116667
	Dec     -19.7166667
	Dist     1755
	Radius   1.276
	Age      48.42
}

Cluster	"Mamajek 4"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.4333333
	Dec     -51
	Dist     385
	Radius   4.704
	Age      631
}

Cluster	"Bica 3"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.4344444
	Dec     -13.0588889
	Dist     1640
	Radius   0.8348
	Age      25.12
}

Cluster	"NGC 6631"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.4530556
	Dec     -12.03
	Dist     2600
	Radius   2.269
	Age      398.1
}

Cluster	"NGC 6633"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.4541667
	Dec      6.50833333
	Dist     376
	Radius   1.094
	Age      425.6
}

Cluster	"Dias 6"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.5083333
	Dec     -12.3163889
	Dist     1580
	Radius   1.379
	Age      602.6
}

Cluster	"NGC 6639"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.5163889
	Dec     -13.155
	Dist     700
	Radius   0.5091
	Age      724.4
}

Cluster	"Ruprecht 141"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.5216667
	Dec     -12.3166667
	Dist     1429
	Radius   1.039
}

Cluster	"M 25/IC 4725"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.5297222
	Dec     -19.1166667
	Dist     620
	Radius   2.615
	Age      92.26
}

Cluster	"Ruprecht 142"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.5363889
	Dec     -12.2297222
	Dist     1735
	Radius   1.665
	Age      398.1
}

Cluster	"Ruprecht 171"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.5363889
	Dec     -16.0497222
	Dist     1140
	Radius   1.89
	Age      3162
}

Cluster	"NGC 6645"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.5436111
	Dec     -16.8855556
	Dist     1245
	Radius   2.68
	Age      398.1
}

Cluster	"NGC 6649"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.5575
	Dec     -10.4033333
	Dist     1369
	Radius   0.9956
	Age      36.81
}

Cluster	"Alessi 40"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.6097222
	Dec     -19.465
	Dist     800
	Radius   6.004
	Age      75.86
}

Cluster	"NGC 6664"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.6102778
	Dec     -7.81333333
	Dist     1164
	Radius   2.032
	Age      14.52
}

Cluster	"IC 4756"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.65
	Dec      5.45
	Dist     484
	Radius   2.745
	Age      500
}

Cluster	"NGC 6683"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.7036111
	Dec     -6.21166667
	Dist     1197
	Radius   0.5223
	Age      10
}

Cluster	"ASCC 98"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.71
	Dec     -33.63
	Dist     800
	Radius   9.076
	Age      213.8
}

Cluster	"Trumpler 35"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.715
	Dec     -4.13333333
	Dist     1206
	Radius   0.877
	Age      72.78
}

Cluster	"Berkeley 79"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.7533333
	Dec     -1.21666667
	Dist     2300
	Radius   2.007
	Age      64.57
}

Cluster	"M 26/NGC 6694"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.755
	Dec     -9.38333333
	Dist     1600
	Radius   1.629
	Age      85.31
}

Cluster	"Basel 1"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.8033333
	Dec     -5.85
	Dist     2178
	Radius   1.584
	Age      78.16
}

Cluster	"ASCC 99"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.8180556
	Dec     -18.73
	Dist     280
	Radius   1.124
	Age      512.9
}

Cluster	"Ruprecht 145"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.8433333
	Dec     -18.25
	Dist     666
	Radius   3.39
}

Cluster	"NGC 6704"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.8458333
	Dec     -5.205
	Dist     2974
	Radius   2.163
	Age      72.95
}

Cluster	"M 11/NGC 6705"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.8513889
	Dec     -6.27
	Dist     1877
	Radius   8.736
	Age      251.2
}

Cluster	"NGC 6709"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.855
	Dec      10.3183333
	Dist     1075
	Radius   2.189
	Age      150.7
}

Cluster	"Collinder 394"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.8711111
	Dec     -20.2033333
	Dist     690
	Radius   2.208
	Age      63.53
}

Cluster	"Stephenson 1"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.8916667
	Dec      36.9166667
	Dist     390
	Radius   1.134
	Age      53.83
}

Cluster	"Berkeley 80"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.9061111
	Dec     -1.21666667
	Dist     1445
	Radius   0.8407
	Age      707.9
}

Cluster	"NGC 6716"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.9094444
	Dec     -19.9016667
	Dist     789
	Radius   1.148
	Age      91.41
}

Cluster	"ESO 524-01"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.9436111
	Dec     -26.9608333
	Dist     2800
	Radius   2.443
	Age      3162
}

Cluster	"NGC 6728"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       18.9788889
	Dec     -8.97
	Dist     1000
	Radius   2.269
	Age      851.1
}

Cluster	"NGC 6738"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       19.0225
	Dec      11.615
	Dist     700
	Radius   1.527
	Age      1445
}

Cluster	"ASCC 100"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       19.0269444
	Dec      33.57
	Dist     350
	Radius   1.894
	Age      102.3
}

Cluster	"Berkeley 81"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       19.0277778
	Dec      0.456111111
	Dist     3000
	Radius   2.182
	Age      1000
}

Cluster	"NGC 6737"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       19.0388889
	Dec     -18.5497222
	Dist     2120
	Radius   2.713
	Age      501.2
}

Cluster	"Alessi 56"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       19.1144444
	Dec      9.58333333
	Dist     3900
	Radius   1.248
}

Cluster	"NGC 6755"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       19.1302778
	Dec      4.26666667
	Dist     1421
	Radius   2.893
	Age      52.36
}

Cluster	"NGC 6756"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       19.145
	Dec      4.705
	Dist     1507
	Radius   0.8767
	Age      61.66
}

Cluster	"Berkeley 82"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       19.1888889
	Dec      13.1183333
	Dist     860
	Radius   0.2502
	Age      31.12
}

Cluster	"ASCC 101"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       19.2269444
	Dec      36.33
	Dist     350
	Radius   2.199
	Age      331.1
}

Cluster	"ESO 282-26"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       19.2311111
	Dec     -42.6483333
	Dist     1400
	Radius   3.054
	Age      1288
}

Cluster	"Berkeley 43"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       19.26
	Dec      11.2166667
	Dist     1578
	Radius   1.148
	Age      1413
}

Cluster	"Ruprecht 147"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       19.2783333
	Dec     -16.2833333
	Dist     174
	Radius   0.6327
}

Cluster	"Berkeley 44"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       19.2866667
	Dec      19.55
	Dist     1800
	Radius   2.618
	Age      1288
}

Cluster	"NGC 6791"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       19.3480556
	Dec      37.7716667
	Dist     5853
	Radius   8.513
	Age      4395
}

Cluster	"Alessi 57"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       19.3483333
	Dec      15.6766667
	Dist     3900
	Radius   1.418
}

Cluster	"NGC 6793"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       19.3872222
	Dec      22.1416667
	Dist     909
	Radius   0.7933
}

Cluster	"ASCC 102"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       19.4138889
	Dec      29.95
	Dist     1500
	Radius   4.712
	Age      616.6
}

Cluster	"NGC 6800"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       19.4519444
	Dec      25.14
	Dist     1000
	Radius   0.7272
}

Cluster	"ESO 525-08"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       19.4544444
	Dec     -23.5763889
	Dist     1640
	Radius   1.431
	Age      1000
}

Cluster	"King 26"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       19.4836111
	Dec      14.8672222
	Dist     2600
	Radius   1.664
	Age      436.5
}

Cluster	"Teutsch 42"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       19.5036111
	Dec      18.5358333
	Dist     1600
	Radius   0.2793
	Age      31.62
}

Cluster	"NGC 6802"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       19.5097222
	Dec      20.2616667
	Dist     1124
	Radius   0.8174
	Age      741.3
}

Cluster	"Kronberger 79"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       19.5652778
	Dec      18.52
	Dist     2700
	Radius   0.8247
	Age      223.9
}

Cluster	"Stock 1"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       19.5966667
	Dec      25.2166667
	Dist     318
	Radius   2.405
	Age      302
}

Cluster	"Teutsch 35"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       19.5988889
	Dec      35.6683333
	Dist     700
	Radius   4.398
	Age      234.4
}

Cluster	"NGC 6811"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       19.6213889
	Dec      46.3883333
	Dist     1215
	Radius   2.474
	Age      629.5
}

Cluster	"ASCC 104"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       19.6480556
	Dec      18.69
	Dist     800
	Radius   6.702
	Age      51.29
}

Cluster	"Kronberger 31"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       19.6697222
	Dec      26.2633333
	Dist     11900
	Radius   2.25
}

Cluster	"NGC 6819"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       19.6883333
	Dec      40.1866667
	Dist     2360
	Radius   1.716
	Age      1493
}

Cluster	"ASCC 105"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       19.6961111
	Dec      27.38
	Dist     500
	Radius   5.236
	Age      100
}

Cluster	"Alessi 44"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       19.7091667
	Dec      1.52305556
	Dist     500
	Radius   5.76
	Age      263
}

Cluster	"Czernik 40"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       19.71
	Dec      21.1538889
	Dist     3090
	Radius   6.472
	Age      794.3
}

Cluster	"Teutsch 43"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       19.7130556
	Dec      29.8555556
	Dist     8100
	Radius   1.532
}

Cluster	"NGC 6823"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       19.7191667
	Dec      23.3
	Dist     3176
	Radius   2.772
	Age      3.162
}

Cluster	"Turner 9"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       19.7469444
	Dec      29.265
	Dist     852
	Radius   4.089
	Age      158.5
}

Cluster	"Roslund 1"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       19.75
	Dec      17.5166667
	Dist     833
	Radius   0.3635
}

Cluster	"Kronberger 4"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       19.7530556
	Dec      28.1611111
	Dist     7900
	Radius   1.494
}

Cluster	"Roslund 2"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       19.7566667
	Dec      23.9166667
	Dist     1667
	Radius   3.394
}

Cluster	"Turner 1"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       19.8069444
	Dec      27.2930556
	Dist     1675
	Radius   0.9745
	Age      28.84
}

Cluster	"ASCC 107"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       19.8088889
	Dec      21.96
	Dist     700
	Radius   1.344
	Age      257
}

Cluster	"NGC 6827"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       19.8147222
	Dec      21.215
	Dist     4100
	Radius   1.789
	Age      794.3
}

Cluster	"Dias 7"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       19.8225
	Dec      21.17
	Dist     2540
	Radius   3.694
	Age      1995
}

Cluster	"NGC 6828"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       19.8380556
	Dec      7.90333333
	Dist     600
	Radius   0.7854
	Age      363.1
}

Cluster	"NGC 6830"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       19.8497222
	Dec      23.1
	Dist     1639
	Radius   1.192
	Age      37.33
}

Cluster	"Czernik 41"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       19.8502778
	Dec      25.2686111
	Dist     1360
	Radius   0.8703
	Age      501.2
}

Cluster	"Saurer 6"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       19.8505556
	Dec      32.2430556
	Dist     9330
	Radius   2.443
	Age      1950
}

Cluster	"Dias 8"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       19.8683333
	Dec      11.6344444
	Dist     2220
	Radius   3.229
	Age      2239
}

Cluster	"NGC 6834"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       19.87
	Dec      29.4083333
	Dist     2067
	Radius   1.503
	Age      76.38
}

Cluster	"Harvard 20"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       19.885
	Dec      18.3333333
	Dist     1540
	Radius   1.568
	Age      29.92
}

Cluster	"ASCC 108"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       19.8969444
	Dec      39.37
	Dist     1100
	Radius   4.224
	Age      436.5
}

Cluster	"ASCC 109"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       19.9
	Dec      34.58
	Dist     450
	Radius   3.927
	Age      204.2
}

Cluster	"Loiano 1"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       19.9705556
	Dec      32.545
	Dist     2700
	Radius   2.356
	Age      251.2
}

Cluster	"Roslund 3"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       19.9783333
	Dec      20.4833333
	Dist     1467
	Radius   1.067
	Age      108.6
}

Cluster	"Berkeley 83"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       20.0216667
	Dec      28.6166667
	Dist     5728
	Radius   1.666
	Age      891.3
}

Cluster	"Teutsch 8"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       20.0397222
	Dec      35.3113889
	Dist     1600
	Radius   0.256
	Age      10
}

Cluster	"Dolidze 36"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       20.0416667
	Dec      42.1
	Dist     900
	Radius   1.833
	Age      676.1
}

Cluster	"ASCC 110"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       20.05
	Dec      33.57
	Dist     800
	Radius   3.211
	Age      562.3
}

Cluster	"NGC 6866"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       20.0652778
	Dec      44.1583333
	Dist     1450
	Radius   2.953
	Age      376.7
}

Cluster	"Alessi 10"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       20.0794444
	Dec     -10.4783333
	Dist     513
	Radius   1.343
	Age      223.9
}

Cluster	"Roslund 4"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       20.0816667
	Dec      29.2166667
	Dist     2000
	Radius   1.454
	Age      3.981
}

Cluster	"NGC 6863"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       20.0852778
	Dec     -3.55555556
	Dist     1200
	Radius   0.5236
	Age      3467
}

Cluster	"Dolidze 38"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       20.095
	Dec      41.2
	Dist     1200
	Radius   3.142
	Age      891.3
}

Cluster	"NGC 6871"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       20.0997222
	Dec      35.7766667
	Dist     1574
	Radius   6.639
	Age      9.078
}

Cluster	"Basel 6"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       20.1133333
	Dec      38.35
	Dist     1548
	Radius   1.576
	Age      94.84
}

Cluster	"Biurakan 1"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       20.125
	Dec      35.6833333
	Dist     1667
	Radius   2.425
}

Cluster	"Biurakan 2"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       20.1533333
	Dec      35.4833333
	Dist     1106
	Radius   3.217
	Age      10.26
}

Cluster	"Roslund 5"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       20.1666667
	Dec      33.7666667
	Dist     389
	Radius   2.829
	Age      67.92
}

Cluster	"IC 1311"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       20.1716667
	Dec      41.2166667
	Dist     6026
	Radius   4.382
	Age      1585
}

Cluster	"ASCC 111"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       20.1869444
	Dec      37.45
	Dist     1600
	Radius   15.36
	Age      11.22
}

Cluster	"NGC 6883"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       20.1886111
	Dec      35.8316667
	Dist     1380
	Radius   7.025
}

Cluster	"Ruprecht 172"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       20.1933333
	Dec      35.605
	Dist     1100
	Radius   2.4
	Age      812.8
}

Cluster	"NGC 6885"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       20.2002778
	Dec      26.4783333
	Dist     597
	Radius   1.737
	Age      1445
}

Cluster	"Berkeley 52"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       20.2416667
	Dec      28.9466667
	Dist     4900
	Radius   2.138
	Age      1995
}

Cluster	"Dolidze 39"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       20.2733333
	Dec      37.8666667
	Dist     1429
	Radius   2.494
}

Cluster	"IC 4996"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       20.2752778
	Dec      37.6552778
	Dist     2398
	Radius   0.7673
	Age      7.413
}

Cluster	"Alessi-Teutsch 11"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       20.2755556
	Dec      52.075
	Dist     550
	Radius   2.112
	Age      141.3
}

Cluster	"Collinder 419"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       20.3019444
	Dec      40.7319444
	Dist     769
	Radius   0.4474
}

Cluster	"Berkeley 85"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       20.3152778
	Dec      37.7591667
	Dist     1760
	Radius   1.28
	Age      1000
}

Cluster	"Dolidze 42"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       20.3283333
	Dec      38.1333333
	Dist     972
	Radius   0.9896
	Age      34.83
}

Cluster	"Berkeley 86"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       20.34
	Dec      38.7
	Dist     1112
	Radius   0.9704
	Age      13.06
}

Cluster	"Berkeley 87"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       20.3616667
	Dec      37.3666667
	Dist     633
	Radius   0.9207
	Age      14.19
}

Cluster	"Collinder 421"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       20.3861111
	Dec      41.6930556
	Dist     1050
	Radius   0.5498
	Age      251.2
}

Cluster	"NGC 6910"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       20.3866667
	Dec      40.7783333
	Dist     1139
	Radius   1.657
	Age      13.4
}

Cluster	"M 29/NGC 6913"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       20.3991667
	Dec      38.5083333
	Dist     1148
	Radius   1.67
	Age      12.91
}

Cluster	"Teutsch 30"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       20.4619444
	Dec      36.0755556
	Dist     1600
	Radius   0.7447
	Age      7.943
}

Cluster	"Roslund 6"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       20.4802778
	Dec      39.3266667
	Dist     500
	Radius   1.745
}

Cluster	"NGC 6939"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       20.525
	Dec      60.6616667
	Dist     1800
	Radius   2.618
	Age      1585
}

Cluster	"Bica 1"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       20.5527778
	Dec      41.2186111
	Dist     1800
	Radius   0.7854
	Age      3.981
}

Cluster	"Bica 2"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       20.5541667
	Dec      41.3125
	Dist     1800
	Radius   1.309
	Age      3.981
}

Cluster	"NGC 6940"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       20.5738889
	Dec      28.2833333
	Dist     770
	Radius   2.8
	Age      721.1
}

Cluster	"Alessi 12"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       20.73
	Dec      23.7916667
	Dist     537
	Radius   3.124
	Age      125.9
}

Cluster	"Roslund 7"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       20.8688889
	Dec      37.8955556
	Dist     571
	Radius   1.661
}

Cluster	"Barkhatova 1"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       20.895
	Dec      46.0333333
	Dist     833
	Radius   2.423
}

Cluster	"NGC 6991"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       20.915
	Dec      47.4166667
	Dist     700
	Radius   2.545
	Age      1288
}

Cluster	"NGC 6996"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       20.9416667
	Dec      44.6333333
	Dist     760
	Radius   1.548
	Age      346.7
}

Cluster	"Berkeley 54"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       21.0533333
	Dec      40.4666667
	Dist     2300
	Radius   1.338
	Age      3981
}

Cluster	"Collinder 428"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       21.0533333
	Dec      44.5833333
	Dist     1000
	Radius   1.454
}

Cluster	"NGC 7031"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       21.12
	Dec      50.875
	Dist     900
	Radius   1.833
	Age      137.4
}

Cluster	"NGC 7036"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       21.1672222
	Dec      15.5166667
	Dist     1000
	Radius   0.5818
	Age      3162
}

Cluster	"Basel 12"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       21.175
	Dec      46.2333333
	Dist     1466
	Radius   0.8529
	Age      316.2
}

Cluster	"NGC 7039"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       21.18
	Dec      45.6166667
	Dist     951
	Radius   1.936
	Age      66.07
}

Cluster	"ASCC 113"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       21.2
	Dec      38.6
	Dist     450
	Radius   3.691
	Age      138
}

Cluster	"IC 1369"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       21.2016667
	Dec      47.7333333
	Dist     2083
	Radius   1.515
	Age      436.5
}

Cluster	"Basel 13"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       21.205
	Dec      46.5666667
	Dist     1236
	Radius   1.798
	Age      631
}

Cluster	"NGC 7044"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       21.2191667
	Dec      42.495
	Dist     3161
	Radius   2.758
	Age      1901
}

Cluster	"Basel 15"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       21.265
	Dec      48.85
	Dist     1355
	Radius   1.182
	Age      316.2
}

Cluster	"Berkeley 55"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       21.2827778
	Dec      51.7588889
	Dist     1210
	Radius   0.7391
	Age      316.2
}

Cluster	"Berkeley 56"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       21.295
	Dec      41.8266667
	Dist     12100
	Radius   3.52
	Age      3981
}

Cluster	"Basel 14"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       21.355
	Dec      44.8166667
	Dist     964
	Radius   0.701
	Age      199.5
}

Cluster	"NGC 7058"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       21.3647222
	Dec      50.8183333
	Dist     400
	Radius   0.4072
	Age      223.9
}

Cluster	"NGC 7062"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       21.3908333
	Dec      46.3783333
	Dist     1480
	Radius   1.076
	Age      291.7
}

Cluster	"NGC 7063"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       21.4058333
	Dec      36.4866667
	Dist     689
	Radius   0.9019
	Age      94.84
}

Cluster	"NGC 7067"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       21.4063889
	Dec      48.01
	Dist     3600
	Radius   3.142
	Age      100
}

Cluster	"Berkeley 92"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       21.4130556
	Dec      57.5333333
	Dist     8630
	Radius   2.51
	Age      3162
}

Cluster	"Kronberger 81"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       21.4358333
	Dec      53.5327778
	Dist     5900
	Radius   3.003
}

Cluster	"NGC 7082"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       21.4880556
	Dec      47.1266667
	Dist     1442
	Radius   5.243
	Age      171
}

Cluster	"Platais 1"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       21.5005556
	Dec      48.9766667
	Dist     1268
	Radius   1.844
	Age      175.4
}

Cluster	"NGC 7086"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       21.5075
	Dec      51.6
	Dist     1298
	Radius   2.265
	Age      138.7
}

Cluster	"M 39/NGC 7092"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       21.53
	Dec      48.4333333
	Dist     326
	Radius   1.375
	Age      278.6
}

Cluster	"Trumpler 37"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       21.6516667
	Dec      57.5
	Dist     835
	Radius   10.81
	Age      11.32
}

Cluster	"ASCC 114"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       21.6669444
	Dec      53.97
	Dist     550
	Radius   1.536
	Age      56.23
}

Cluster	"NGC 7128"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       21.7325
	Dec      53.715
	Dist     2307
	Radius   1.342
	Age      17.95
}

Cluster	"NGC 7142"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       21.7525
	Dec      65.775
	Dist     1686
	Radius   2.943
	Age      1888
}

Cluster	"IC 5146"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       21.89
	Dec      47.2666667
	Dist     852
	Radius   2.478
	Age      1
}

Cluster	"NGC 7160"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       21.8944444
	Dec      62.6033333
	Dist     789
	Radius   0.5738
	Age      18.97
}

Cluster	"Berkeley 93"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       21.9366667
	Dec      63.9333333
	Dist     5600
	Radius   1.629
	Age      100
}

Cluster	"ASCC 115"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       21.9480556
	Dec      51.48
	Dist     600
	Radius   1.676
	Age      389
}

Cluster	"ASCC 116"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       21.9761111
	Dec      54.49
	Dist     5000
	Radius   15.71
	Age      10.72
}

Cluster	"ASCC 117"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       22.0830556
	Dec      62.27
	Dist     1200
	Radius   7.33
	Age      4.677
}

Cluster	"NGC 7209"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       22.0852778
	Dec      46.4833333
	Dist     1168
	Radius   2.378
	Age      414
}

Cluster	"Alessi-Teutsch 5"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       22.1408333
	Dec      61.0283333
	Dist     900
	Radius   3.299
	Age      10.47
}

Cluster	"NGC 7226"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       22.1738889
	Dec      55.3983333
	Dist     2616
	Radius   0.761
	Age      272.9
}

Cluster	"IC 1434"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       22.1761111
	Dec      52.8277778
	Dist     3035
	Radius   3.09
	Age      316.2
}

Cluster	"NGC 7235"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       22.2069444
	Dec      57.27
	Dist     3330
	Radius   2.422
	Age      7.943
}

Cluster	"NGC 7243"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       22.2522222
	Dec      49.8983333
	Dist     808
	Radius   3.408
	Age      114.3
}

Cluster	"NGC 7245"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       22.2530556
	Dec      54.3433333
	Dist     3800
	Radius   2.763
	Age      398.1
}

Cluster	"King 9"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       22.2583333
	Dec      54.4
	Dist     7900
	Radius   3.447
	Age      3162
}

Cluster	"IC 1442"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       22.275
	Dec      54.05
	Dist     2346
	Radius   1.706
	Age      9.594
}

Cluster	"Pismis-Moreno 1"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       22.3133333
	Dec      63.2666667
	Dist     900
	Radius   0.7854
}

Cluster	"ASCC 119"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       22.32
	Dec      46.9
	Dist     1000
	Radius   3.665
	Age      676.1
}

Cluster	"NGC 7261"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       22.3363889
	Dec      58.1216667
	Dist     1681
	Radius   1.222
	Age      46.77
}

Cluster	"Berkeley 94"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       22.3783333
	Dec      55.85
	Dist     2630
	Radius   1.148
	Age      9.908
}

Cluster	"NGC 7281"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       22.4216667
	Dec      57.8166667
	Dist     833
	Radius   1.454
}

Cluster	"Berkeley 96"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       22.49
	Dec      55.4
	Dist     3087
	Radius   0.898
	Age      6.637
}

Cluster	"ASCC 120"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       22.51
	Dec      57.21
	Dist     2500
	Radius   13.09
	Age      12.02
}

Cluster	"ASCC 121"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       22.5119444
	Dec      54.9
	Dist     2500
	Radius   10.04
	Age      53.7
}

Cluster	"ASCC 122"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       22.5538889
	Dec      39.61
	Dist     700
	Radius   8.797
	Age      9.55
}

Cluster	"ASCC 123"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       22.71
	Dec      54.26
	Dist     250
	Radius   5.586
	Age      257
}

Cluster	"Berkeley 98"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       22.7105556
	Dec      52.3877778
	Dist     3739
	Radius   5.438
	Age      2512
}

Cluster	"NGC 7380"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       22.7891667
	Dec      58.1316667
	Dist     2222
	Radius   6.464
	Age      11.94
}

Cluster	"Alessi 37"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       22.8022222
	Dec      46.2516667
	Dist     600
	Radius   3.142
	Age      302
}

Cluster	"King 18"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       22.8686111
	Dec      58.2825
	Dist     1860
	Radius   1.299
	Age      346.7
}

Cluster	"NGC 7419"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       22.9055556
	Dec      60.815
	Dist     2300
	Radius   1.673
	Age      14.13
}

Cluster	"King 10"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       22.915
	Dec      59.1666667
	Dist     3379
	Radius   1.966
	Age      27.93
}

Cluster	"NGC 7423"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       22.9188889
	Dec      57.0966667
	Dist     4150
	Radius   3.018
	Age      1413
}

Cluster	"ASCC 125"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       22.9380556
	Dec      62.75
	Dist     1500
	Radius   14.4
	Age      10.23
}

Cluster	"NGC 7438"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       22.9561111
	Dec      54.3383333
	Dist     600
	Radius   0.6981
	Age      851.1
}

Cluster	"ASCC 126"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       23.105
	Dec      51.05
	Dist     800
	Radius   4.189
	Age      18.2
}

Cluster	"King 19"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       23.1383333
	Dec      60.5166667
	Dist     1967
	Radius   1.43
	Age      360.6
}

Cluster	"ASCC 127"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       23.14
	Dec      64.85
	Dist     350
	Radius   4.398
	Age      66.07
}

Cluster	"NGC 7510"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       23.1841667
	Dec      60.57
	Dist     3480
	Radius   3.037
	Age      22.39
}

Cluster	"Markarian 50"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       23.255
	Dec      60.4666667
	Dist     2114
	Radius   0.6149
	Age      12.45
}

Cluster	"ASCC 128"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       23.3430556
	Dec      54.6
	Dist     900
	Radius   5.498
	Age      275.4
}

Cluster	"Berkeley 99"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       23.36
	Dec      71.75
	Dist     4900
	Radius   3.563
	Age      3162
}

Cluster	"M 52/NGC 7654"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       23.4133333
	Dec      61.5933333
	Dist     1400
	Radius   3.054
	Age      158.5
}

Cluster	"Czernik 43"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       23.43
	Dec      61.3166667
	Dist     2510
	Radius   1.825
	Age      39.81
}

Cluster	"Alessi J2327+55"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       23.4613889
	Dec      55.5916667
	Dist     1000
	Radius   3.665
	Age      489.8
}

Cluster	"King 20"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       23.5541667
	Dec      58.4666667
	Dist     1900
	Radius   1.658
	Age      199.5
}

Cluster	"Stock 12"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       23.6055556
	Dec      52.5433333
	Dist     476
	Radius   2.423
}

Cluster	"Aveni-Hunter 1"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       23.6297222
	Dec      48.56
	Dist     500
	Radius   3.418
}

Cluster	"Berkeley 102"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       23.645
	Dec      56.6333333
	Dist     9638
	Radius   7.009
	Age      3162
}

Cluster	"Stock 17"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       23.7297222
	Dec      62.1602778
	Dist     2144
	Radius   0.3118
	Age      5.957
}

Cluster	"Berkeley 103"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       23.7533333
	Dec      59.3
	Dist     7379
	Radius   2.146
	Age      891.3
}

Cluster	"Negueruela 1"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       23.7888889
	Dec      63.22
	Dist     2511
	Radius   0.7304
}

Cluster	"King 11"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       23.7966667
	Dec      68.6333333
	Dist     2892
	Radius   2.103
	Age      1117
}

Cluster	"King 21"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       23.8316667
	Dec      62.7166667
	Dist     2103
	Radius   1.223
	Age      14.55
}

Cluster	"Pfleiderer 4"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       23.8486111
	Dec      62.3208333
	Dist     7900
	Radius   4.596
	Age      6310
}

Cluster	"NGC 7772"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       23.8627778
	Dec      16.2466667
	Dist     1500
	Radius   0.6545
	Age      1479
}

Cluster	"ASCC 130"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       23.88
	Dec      62.44
	Dist     3400
	Radius   8.901
	Age      10.72
}

Cluster	"King 12"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       23.8833333
	Dec      61.9666667
	Dist     2378
	Radius   1.038
	Age      10.89
}

Cluster	"NGC 7788"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       23.9458333
	Dec      61.3983333
	Dist     2374
	Radius   1.381
	Age      39.17
}

Cluster	"NGC 7789"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       23.9566667
	Dec      56.7083333
	Dist     1795
	Radius   6.527
	Age      1413
}

Cluster	"Frolov 1"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       23.9566667
	Dec      61.6333333
	Dist     2560
	Radius   1.489
	Age      45.71
}

Cluster	"NGC 7790"
{
	Galaxy  "Milky Way"
	Type    "Open"
	RA       23.9733333
	Dec      61.2083333
	Dist     2944
	Radius   2.141
	Age      56.1
}
