# Mission Aware (CPS) MBSE Meta-Model

## About the paper

Georgios Bakirtzis, Tim Sherburne, Stephen Adams, Barry M. Horowitz, Peter A. Beling, and Cody H. Fleming 
"An Ontological Metamodel for Cyber-Physical System Safety, Security, and Resilience Coengineering",
[preprint](https://arxiv.org/pdf/2006.05304.pdf)

## Mission Aware Inputs
![MA-Systemigraph](/images/ma-context.png)
## MBSE Meta-Model
A representation of critical systems engineering concepts and their interrelationships spanning _**requirements**_, _**behavior**_, _**architecture**_, and _**test**_.  This integrated model presents a high-level view of not only the ultimate specification of a system, but also the journey to that specification – _**concerns**_ opened and closed, _**risks**_ identified and managed.

![MBSE](images/mbse.png)

### References
* [MBSE](http://www.vitechcorp.com/resources/white_papers/onemodel.pdf)
* [Hazard Analysis - STPA](http://psas.scripts.mit.edu/home/get_file.php?name=STPA_handbook.pdf)

## CPS Meta-Model
![MA Model](/images/ma-mbse.png)

### References
* [SERC Overview Slides](/presentation/MissionAwareOverview.pdf)
* [Journal Paper](https://github.com/coordinated-systems-lab/cps-metamodel)

## CPS Schema
<img src="images/graphql.png" width="200">

* [graphql overview](https://graphql.org/)
* [cps-metamodel.graphql](/schema/cps-metamodel.graphql)
* [sample-queries](https://gist.github.com/tsherburne/3d3fd799771016ff0535388e1145b56e)

## Sample Project Models

* [pipeline-cps.json](https://github.com/coordinated-systems-lab/pipeline-cps/blob/master/export/pipeline-cps.json)
* [silverfish.json](https://gitlab.com/tsherburne/silverfish-mbse/blob/master/mbse/silverfish.json)
